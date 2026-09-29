using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Services.Interfaces;

public interface IInventoryService
{
    List<ProductCategory> GetAllCategories();
    List<Unit> GetAllUnits();
    List<Product> GetAllProducts(int? categoryId = null, string? search = null);
    Product? GetProductById(int productId);

    bool SaveProduct(Product product, int initialStock = 0);
    bool DeleteProduct(int productId);

    bool OrderProductForSession(int sessionId, int productId, int quantity, int userId);
    bool CompleteOrderItem(int orderItemId, int userId);
    bool CancelOrderItem(int orderItemId, int userId, string reason);
    List<OrderItem> GetOrderItemsForQueue(bool onlyPending = false);
    List<OrderItem> GetOrderItemsBySession(int sessionId);

    bool ImportStock(int productId, int quantity, decimal unitCost, int userId, string? note = null);
    List<Product> GetLowStockAlerts();
}
