using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public class OrderCartItem : ObservableObject
{
    public Product Product { get; }
    
    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(TotalAmount));
            }
        }
    }

    public decimal UnitPrice => Product.SellingPrice;
    public decimal TotalAmount => UnitPrice * Quantity;

    public OrderCartItem(Product product, int quantity = 1)
    {
        Product = product;
        Quantity = quantity;
    }
}

public partial class OrderDialogViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private int _sessionId;

    [ObservableProperty]
    private TableItemViewModel _tableItem;

    [ObservableProperty]
    private ObservableCollection<TableItemViewModel> _activeTables = new();

    [ObservableProperty]
    private TableItemViewModel? _selectedActiveTable;

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categories = new();

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<Product> _products = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<OrderCartItem> _cartItems = new();

    [ObservableProperty]
    private ObservableCollection<OrderItem> _existingOrderItems = new();

    [ObservableProperty]
    private decimal _cartTotal;

    [ObservableProperty]
    private string? _message;

    public event Action? RequestClose;

    public OrderDialogViewModel(
        IInventoryService inventoryService, 
        IAuthService authService, 
        TableItemViewModel tableItem,
        IEnumerable<TableItemViewModel>? allActiveTables = null)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        _tableItem = tableItem;
        _sessionId = tableItem.Model.CurrentSessionId ?? 0;

        if (allActiveTables != null && allActiveTables.Any())
        {
            ActiveTables = new ObservableCollection<TableItemViewModel>(allActiveTables);
            SelectedActiveTable = ActiveTables.FirstOrDefault(t => t.TableId == tableItem.TableId) ?? tableItem;
        }
        else
        {
            ActiveTables = new ObservableCollection<TableItemViewModel> { tableItem };
            SelectedActiveTable = tableItem;
        }

        LoadCategoriesAndProducts();
        LoadExistingOrders();
    }

    partial void OnSelectedActiveTableChanged(TableItemViewModel? value)
    {
        if (value != null && value.TableId != TableItem.TableId)
        {
            TableItem = value;
            _sessionId = value.Model.CurrentSessionId ?? 0;
            LoadExistingOrders();
            Message = $"⚡ Đã chuyển sang gọi món cho {value.TableNumber} ({value.AreaName})";
        }
    }

    private void LoadCategoriesAndProducts()
    {
        var cats = _inventoryService.GetAllCategories();
        var allCats = new List<ProductCategory>
        {
            new ProductCategory { CategoryId = 0, CategoryName = "── Tất Cả Danh Mục ──" }
        };
        allCats.AddRange(cats);
        Categories = new ObservableCollection<ProductCategory>(allCats);
        SelectedCategory = Categories.FirstOrDefault();
        FilterProducts();
    }

    public void LoadExistingOrders()
    {
        if (TableItem.Model.CurrentSession?.OrderItems != null)
        {
            ExistingOrderItems = new ObservableCollection<OrderItem>(
                TableItem.Model.CurrentSession.OrderItems.Where(o => !o.IsCancelled)
            );
        }
        else
        {
            ExistingOrderItems = new ObservableCollection<OrderItem>();
        }
    }

    partial void OnSelectedCategoryChanged(ProductCategory? value) => FilterProducts();
    partial void OnSearchKeywordChanged(string value) => FilterProducts();

    private void FilterProducts()
    {
        int? catId = (SelectedCategory != null && SelectedCategory.CategoryId > 0) ? SelectedCategory.CategoryId : null;
        var items = _inventoryService.GetAllProducts(catId, SearchKeyword);
        Products = new ObservableCollection<Product>(items);
    }

    [RelayCommand]
    private void AddToCart(Product? product)
    {
        if (product == null) return;

        // Kiểm tra tồn kho nếu là mặt hàng quản lý kho
        if (product.IsInventoryItem)
        {
            int currentInCart = CartItems.FirstOrDefault(c => c.Product.ProductId == product.ProductId)?.Quantity ?? 0;
            int availableStock = product.InventoryItem?.QuantityOnHand ?? 0;

            if (currentInCart + 1 > availableStock)
            {
                Message = $"Sản phẩm {product.ProductName} chỉ còn {availableStock} trong kho!";
                return;
            }
        }

        var existingInCart = CartItems.FirstOrDefault(c => c.Product.ProductId == product.ProductId);
        if (existingInCart != null)
        {
            existingInCart.Quantity++;
        }
        else
        {
            CartItems.Add(new OrderCartItem(product, 1));
        }

        UpdateCartTotal();
        Message = null;
    }

    [RelayCommand]
    private void IncreaseQuantity(OrderCartItem? item)
    {
        if (item == null) return;
        if (item.Product.IsInventoryItem)
        {
            int availableStock = item.Product.InventoryItem?.QuantityOnHand ?? 0;
            if (item.Quantity + 1 > availableStock)
            {
                Message = $"Mặt hàng {item.Product.ProductName} đã đạt tối đa tồn kho ({availableStock})!";
                return;
            }
        }
        item.Quantity++;
        UpdateCartTotal();
    }

    [RelayCommand]
    private void DecreaseQuantity(OrderCartItem? item)
    {
        if (item == null) return;
        if (item.Quantity > 1)
        {
            item.Quantity--;
        }
        else
        {
            CartItems.Remove(item);
        }
        UpdateCartTotal();
    }

    [RelayCommand]
    private void RemoveCartItem(OrderCartItem? item)
    {
        if (item != null)
        {
            CartItems.Remove(item);
            UpdateCartTotal();
        }
    }

    private void UpdateCartTotal()
    {
        CartTotal = CartItems.Sum(c => c.TotalAmount);
    }

    [RelayCommand]
    private void ConfirmOrder()
    {
        if (CartItems.Count == 0)
        {
            Message = "⚠️ Giỏ gọi món đang trống! Vui lòng bấm '+ Thêm' món trước khi xác nhận.";
            return;
        }

        int userId = _authService.CurrentUser?.UserId ?? 1;

        try
        {
            int count = 0;
            foreach (var item in CartItems)
            {
                _inventoryService.OrderProductForSession(_sessionId, item.Product.ProductId, item.Quantity, userId);
                count += item.Quantity;
            }

            Message = $"✅ Đã thêm {count} món cho {TableItem.TableNumber} thành công!";
            CartItems.Clear();
            UpdateCartTotal();

            // Nạp lại danh sách món đã phục vụ và cập nhật tiền bàn
            LoadExistingOrders();
            TableItem.UpdateFromModel();
        }
        catch (Exception ex)
        {
            Message = $"Lỗi gọi món: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ConfirmAndClose()
    {
        if (CartItems.Count > 0)
        {
            ConfirmOrder();
        }
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void CancelOrderItem(OrderItem? item)
    {
        if (item == null) return;
        int userId = _authService.CurrentUser?.UserId ?? 1;

        try
        {
            _inventoryService.CancelOrderItem(item.OrderItemId, userId, "Khách yêu cầu hủy món");
            ExistingOrderItems.Remove(item);
            TableItem.UpdateFromModel();
            Message = $"Đã hủy món {item.Product.ProductName} và hoàn trả kho!";
        }
        catch (Exception ex)
        {
            Message = $"Lỗi hủy món: {ex.Message}";
        }
    }
}
