using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    public InventoryService(IDbContextFactory<AppDbContext> contextFactory, IAuditService auditService)
    {
        _contextFactory = contextFactory;
        _auditService = auditService;
    }

    public List<ProductCategory> GetAllCategories()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.ProductCategories.OrderBy(c => c.CategoryName).ToList();
    }

    public bool SaveCategory(ProductCategory category)
    {
        using var context = _contextFactory.CreateDbContext();
        if (category.CategoryId == 0)
        {
            context.ProductCategories.Add(category);
        }
        else
        {
            var existing = context.ProductCategories.Find(category.CategoryId);
            if (existing == null) return false;
            existing.CategoryName = category.CategoryName.Trim();
            existing.IsActive = category.IsActive;
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteCategory(int categoryId, out string errorMessage)
    {
        errorMessage = string.Empty;
        using var context = _contextFactory.CreateDbContext();
        int productCount = context.Products.Count(p => p.CategoryId == categoryId && p.IsActive);
        if (productCount > 0)
        {
            errorMessage = $"Không thể xóa nhóm này vì đang có {productCount} sản phẩm / món đang thuộc danh mục!";
            return false;
        }

        var cat = context.ProductCategories.Find(categoryId);
        if (cat == null) return false;
        context.ProductCategories.Remove(cat);
        return context.SaveChanges() > 0;
    }

    public List<Unit> GetAllUnits()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Units.OrderBy(u => u.UnitName).ToList();
    }

    public bool SaveUnit(Unit unit)
    {
        using var context = _contextFactory.CreateDbContext();
        if (unit.UnitId == 0)
        {
            context.Units.Add(unit);
        }
        else
        {
            var existing = context.Units.Find(unit.UnitId);
            if (existing == null) return false;
            existing.UnitName = unit.UnitName.Trim();
        }
        return context.SaveChanges() > 0;
    }

    public bool DeleteUnit(int unitId, out string errorMessage)
    {
        errorMessage = string.Empty;
        using var context = _contextFactory.CreateDbContext();
        int productCount = context.Products.Count(p => p.UnitId == unitId && p.IsActive);
        if (productCount > 0)
        {
            errorMessage = $"Không thể xóa đơn vị này vì đang có {productCount} sản phẩm đang sử dụng!";
            return false;
        }

        var unit = context.Units.Find(unitId);
        if (unit == null) return false;
        context.Units.Remove(unit);
        return context.SaveChanges() > 0;
    }

    public List<Product> GetAllProducts(int? categoryId = null, string? search = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.Products
                           .Include(p => p.Category)
                           .Include(p => p.Unit)
                           .Include(p => p.InventoryItem)
                           .Where(p => p.IsActive);

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim().ToLower();
            query = query.Where(p => p.ProductName.ToLower().Contains(keyword) || p.ProductCode.ToLower().Contains(keyword));
        }

        return query.OrderBy(p => p.Category.CategoryName).ThenBy(p => p.ProductName).ToList();
    }

    public Product? GetProductById(int productId)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Products
                      .Include(p => p.Category)
                      .Include(p => p.Unit)
                      .Include(p => p.InventoryItem)
                      .FirstOrDefault(p => p.ProductId == productId);
    }

    public bool SaveProduct(Product product, int initialStock = 0)
    {
        using var context = _contextFactory.CreateDbContext();
        if (product.ProductId == 0)
        {
            context.Products.Add(product);
            context.SaveChanges();

            if (product.IsInventoryItem)
            {
                var inv = new InventoryItem
                {
                    ProductId = product.ProductId,
                    QuantityOnHand = initialStock,
                    LastRestockedAt = DateTime.Now
                };
                context.InventoryItems.Add(inv);
                context.SaveChanges();
            }
        }
        else
        {
            context.Products.Update(product);
            context.SaveChanges();
        }
        return true;
    }

    public bool DeleteProduct(int productId)
    {
        using var context = _contextFactory.CreateDbContext();
        var product = context.Products.Find(productId);
        if (product == null) return false;

        product.IsActive = false;
        return context.SaveChanges() > 0;
    }

    public bool OrderProductForSession(int sessionId, int productId, int quantity, int userId)
    {
        if (quantity <= 0) return false;

        using var context = _contextFactory.CreateDbContext();
        var session = context.TableSessions.Find(sessionId);
        var product = context.Products.Include(p => p.InventoryItem).FirstOrDefault(p => p.ProductId == productId);

        if (session == null || product == null || session.Status != SessionStatus.Active) return false;

        // Nếu sản phẩm quản lý tồn kho, kiểm tra số lượng hiện có
        if (product.IsInventoryItem)
        {
            if (product.InventoryItem == null || product.InventoryItem.QuantityOnHand < quantity)
            {
                throw new InvalidOperationException($"Không đủ tồn kho cho món {product.ProductName} (Còn: {product.InventoryItem?.QuantityOnHand ?? 0})");
            }

            // Tự động trừ kho ngay lập tức
            product.InventoryItem.QuantityOnHand -= quantity;
        }

        var orderItem = new OrderItem
        {
            SessionId = sessionId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = product.SellingPrice,
            TotalAmount = product.SellingPrice * quantity,
            OrderedByUserId = userId,
            OrderedAt = DateTime.Now,
            IsCancelled = false
        };

        context.OrderItems.Add(orderItem);

        // Ghi nhận phiếu xuất bán
        if (product.IsInventoryItem)
        {
            var tx = new StockTransaction
            {
                TransactionType = StockTransactionType.ExportSale,
                PerformedByUserId = userId,
                ReferenceCode = $"SESSION-{sessionId}",
                Note = $"Xuất bán tại bàn cho phiên #{sessionId}",
                CreatedAt = DateTime.Now
            };
            context.StockTransactions.Add(tx);
            context.SaveChanges();

            context.StockTransactionDetails.Add(new StockTransactionDetail
            {
                StockTransactionId = tx.StockTransactionId,
                ProductId = productId,
                Quantity = quantity,
                UnitCost = product.CostPrice,
                TotalCost = product.CostPrice * quantity
            });
        }

        context.SaveChanges();

        _auditService.Log(userId, "ORDER_ITEM", "OrderItem", orderItem.OrderItemId.ToString(), null, new
        {
            SessionId = sessionId,
            Product = product.ProductName,
            Quantity = quantity,
            Amount = orderItem.TotalAmount
        });

        return true;
    }

    public bool CancelOrderItem(int orderItemId, int userId, string reason)
    {
        using var context = _contextFactory.CreateDbContext();
        var item = context.OrderItems
                          .Include(o => o.Product)
                          .ThenInclude(p => p.InventoryItem)
                          .FirstOrDefault(o => o.OrderItemId == orderItemId);

        if (item == null || item.IsCancelled) return false;

        item.IsCancelled = true;
        item.CancelledByUserId = userId;
        item.CancelledAt = DateTime.Now;
        item.CancellationReason = reason;

        // Hoàn lại kho nếu là sản phẩm quản lý tồn
        if (item.Product.IsInventoryItem && item.Product.InventoryItem != null)
        {
            item.Product.InventoryItem.QuantityOnHand += item.Quantity;
        }

        context.SaveChanges();

        _auditService.Log(userId, "CANCEL_ORDER_ITEM", "OrderItem", orderItemId.ToString(), new { item.Product.ProductName, item.Quantity }, new { Cancelled = true, Reason = reason });
        return true;
    }

    public bool CompleteOrderItem(int orderItemId, int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var item = context.OrderItems
                          .Include(o => o.Product)
                          .Include(o => o.Session)
                          .ThenInclude(s => s.Table)
                          .FirstOrDefault(o => o.OrderItemId == orderItemId);

        if (item == null || item.IsCancelled) return false;

        item.IsCompleted = true;
        item.CompletedAt = DateTime.Now;
        item.CompletedByUserId = userId;

        context.SaveChanges();

        _auditService.Log(userId, "COMPLETE_ORDER_ITEM", "OrderItem", orderItemId.ToString(), null, new
        {
            Table = item.Session?.Table?.TableNumber,
            Product = item.Product.ProductName,
            Quantity = item.Quantity,
            item.TotalAmount
        });

        return true;
    }

    public List<OrderItem> GetOrderItemsForQueue(bool onlyPending = false)
    {
        using var context = _contextFactory.CreateDbContext();
        var query = context.OrderItems
                           .Include(o => o.Product)
                           .ThenInclude(p => p.Category)
                           .Include(o => o.Session)
                           .ThenInclude(s => s.Table)
                           .Where(o => o.Session.Status == SessionStatus.Active);

        if (onlyPending)
        {
            query = query.Where(o => !o.IsCompleted && !o.IsCancelled);
        }

        // Món nào gọi trước lên trước (FIFO)
        return query.OrderBy(o => o.OrderedAt).ToList();
    }

    public List<OrderItem> GetOrderItemsBySession(int sessionId)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.OrderItems
                      .Include(o => o.Product)
                      .Where(o => o.SessionId == sessionId && !o.IsCancelled)
                      .OrderBy(o => o.OrderedAt)
                      .ToList();
    }

    public bool ImportStock(int productId, int quantity, decimal unitCost, int userId, string? note = null)
    {
        if (quantity <= 0) return false;

        using var context = _contextFactory.CreateDbContext();
        var product = context.Products.Include(p => p.InventoryItem).FirstOrDefault(p => p.ProductId == productId);
        if (product == null) return false;

        if (product.InventoryItem == null)
        {
            product.InventoryItem = new InventoryItem { ProductId = productId, QuantityOnHand = 0 };
            context.InventoryItems.Add(product.InventoryItem);
        }

        product.InventoryItem.QuantityOnHand += quantity;
        product.InventoryItem.LastRestockedAt = DateTime.Now;
        product.CostPrice = unitCost; // Cập nhật giá vốn mới

        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.Import,
            PerformedByUserId = userId,
            ReferenceCode = $"IMPORT-{DateTime.Now:yyyyMMddHHmm}",
            Note = note ?? "Nhập hàng vào kho",
            CreatedAt = DateTime.Now
        };
        context.StockTransactions.Add(tx);
        context.SaveChanges();

        context.StockTransactionDetails.Add(new StockTransactionDetail
        {
            StockTransactionId = tx.StockTransactionId,
            ProductId = productId,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = unitCost * quantity
        });

        context.SaveChanges();

        _auditService.Log(userId, "IMPORT_STOCK", "Product", productId.ToString(), null, new
        {
            Product = product.ProductName,
            Quantity = quantity,
            UnitCost = unitCost
        });

        return true;
    }

    public List<Product> GetLowStockAlerts()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Products
                      .Include(p => p.Category)
                      .Include(p => p.Unit)
                      .Include(p => p.InventoryItem)
                      .Where(p => p.IsActive && p.IsInventoryItem && p.InventoryItem != null && p.InventoryItem.QuantityOnHand <= p.MinStockAlert)
                      .OrderBy(p => p.InventoryItem!.QuantityOnHand)
                      .ToList();
    }
}
