using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categories = new();

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<Product> _products = new();

    [ObservableProperty]
    private ObservableCollection<Product> _lowStockProducts = new();

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    // Form thêm/sửa sản phẩm
    [ObservableProperty]
    private string _formProductCode = string.Empty;

    [ObservableProperty]
    private string _formProductName = string.Empty;

    [ObservableProperty]
    private string? _formImagePath;

    [ObservableProperty]
    private decimal _formCostPrice;

    [ObservableProperty]
    private decimal _formSellingPrice;

    [ObservableProperty]
    private int _formInitialStock = 20;

    [ObservableProperty]
    private int _formMinAlert = 5;

    [ObservableProperty]
    private bool _formIsInventory = true;

    [ObservableProperty]
    private ObservableCollection<Unit> _units = new();

    [ObservableProperty]
    private Unit? _formSelectedUnit;

    [ObservableProperty]
    private ProductCategory? _formSelectedCategory;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isEditing;

    public bool IsAdmin => _authService.CurrentUser?.Role == Domain.Enums.UserRole.Admin;

    // Quản lý nhập kho tùy chỉnh số lượng
    [ObservableProperty]
    private bool _isImportingStock;

    [ObservableProperty]
    private Product? _importingProduct;

    [ObservableProperty]
    private int _importQuantity = 10;

    [ObservableProperty]
    private decimal _importCostPrice;

    [ObservableProperty]
    private string? _importNotes = "Nhập kho bổ sung";

    public InventoryViewModel(IInventoryService inventoryService, IAuthService authService)
    {
        _inventoryService = inventoryService;
        _authService = authService;

        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        OnPropertyChanged(nameof(IsAdmin));
        var cats = _inventoryService.GetAllCategories();
        var allCats = new List<ProductCategory>
        {
            new ProductCategory { CategoryId = 0, CategoryName = "── Tất Cả Danh Mục ──" }
        };
        allCats.AddRange(cats);
        Categories = new ObservableCollection<ProductCategory>(allCats);
        SelectedCategory = Categories.FirstOrDefault();

        var units = _inventoryService.GetAllUnits();
        Units = new ObservableCollection<Unit>(units);

        FormSelectedUnit = Units.FirstOrDefault();
        FormSelectedCategory = Categories.FirstOrDefault(c => c.CategoryId > 0) ?? Categories.FirstOrDefault();

        FilterProducts();
        RefreshAlerts();
    }

    private void RefreshAlerts()
    {
        var alerts = _inventoryService.GetLowStockAlerts();
        LowStockProducts = new ObservableCollection<Product>(alerts);
    }

    partial void OnSelectedCategoryChanged(ProductCategory? value) => FilterProducts();
    partial void OnSearchKeywordChanged(string value) => FilterProducts();

    private void FilterProducts()
    {
        int? catId = (SelectedCategory != null && SelectedCategory.CategoryId > 0) ? SelectedCategory.CategoryId : null;
        var list = _inventoryService.GetAllProducts(catId, SearchKeyword);
        Products = new ObservableCollection<Product>(list);
    }

    [RelayCommand]
    private void BrowseImage()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn ảnh cho món / sản phẩm",
            Filter = "Tệp hình ảnh (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                string outputFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData", "Images", "Products");
                if (!System.IO.Directory.Exists(outputFolder))
                {
                    System.IO.Directory.CreateDirectory(outputFolder);
                }

                string ext = System.IO.Path.GetExtension(ofd.FileName);
                string code = !string.IsNullOrWhiteSpace(FormProductCode) ? FormProductCode.Trim().ToUpper() : "PROD";
                string destFile = System.IO.Path.Combine(outputFolder, $"{code}_{Guid.NewGuid():N}{ext}");

                System.IO.File.Copy(ofd.FileName, destFile, true);
                FormImagePath = destFile;
            }
            catch (Exception ex)
            {
                Message = $"Lỗi chọn ảnh: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ClearImage()
    {
        FormImagePath = null;
    }

    [RelayCommand]
    private void PrepareNewProduct()
    {
        IsEditing = false;
        FormProductCode = string.Empty;
        FormProductName = string.Empty;
        FormImagePath = null;
        FormCostPrice = 0;
        FormSellingPrice = 0;
        FormInitialStock = 20;
        FormMinAlert = 5;
        FormIsInventory = true;
        FormSelectedCategory = Categories.FirstOrDefault();
        FormSelectedUnit = Units.FirstOrDefault();
        Message = null;
    }

    [RelayCommand]
    private void EditProduct(Product? product)
    {
        if (product == null || !IsAdmin) return;
        IsEditing = true;
        SelectedProduct = product;

        FormProductCode = product.ProductCode;
        FormProductName = product.ProductName;
        FormImagePath = product.ImagePath;
        FormCostPrice = product.CostPrice;
        FormSellingPrice = product.SellingPrice;
        FormMinAlert = product.MinStockAlert;
        FormIsInventory = product.IsInventoryItem;
        FormSelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId);
        FormSelectedUnit = Units.FirstOrDefault(u => u.UnitId == product.UnitId);
        Message = null;
    }

    [RelayCommand]
    private void SaveProduct()
    {
        if (!IsAdmin)
        {
            Message = "⚠️ Chỉ Quản trị viên (Admin) mới có quyền thêm mới hoặc sửa thông tin sản phẩm!";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormProductCode) || string.IsNullOrWhiteSpace(FormProductName))
        {
            Message = "Vui lòng nhập mã và tên sản phẩm!";
            return;
        }

        if (FormSelectedCategory == null || FormSelectedUnit == null)
        {
            Message = "Vui lòng chọn danh mục và đơn vị tính!";
            return;
        }

        try
        {
            if (IsEditing && SelectedProduct != null)
            {
                SelectedProduct.ProductCode = FormProductCode.Trim().ToUpper();
                SelectedProduct.ProductName = FormProductName.Trim();
                SelectedProduct.ImagePath = FormImagePath;
                SelectedProduct.CostPrice = FormCostPrice;
                SelectedProduct.SellingPrice = FormSellingPrice;
                SelectedProduct.CategoryId = FormSelectedCategory.CategoryId;
                SelectedProduct.UnitId = FormSelectedUnit.UnitId;
                SelectedProduct.MinStockAlert = FormMinAlert;
                SelectedProduct.IsInventoryItem = FormIsInventory;

                _inventoryService.SaveProduct(SelectedProduct);
                Message = "Cập nhật sản phẩm thành công!";
            }
            else
            {
                var newProduct = new Product
                {
                    ProductCode = FormProductCode.Trim().ToUpper(),
                    ProductName = FormProductName.Trim(),
                    ImagePath = FormImagePath,
                    CostPrice = FormCostPrice,
                    SellingPrice = FormSellingPrice,
                    CategoryId = FormSelectedCategory.CategoryId,
                    UnitId = FormSelectedUnit.UnitId,
                    MinStockAlert = FormMinAlert,
                    IsInventoryItem = FormIsInventory,
                    IsActive = true
                };

                _inventoryService.SaveProduct(newProduct, FormInitialStock);
                Message = "Thêm mới sản phẩm thành công!";
            }

            PrepareNewProduct();
            FilterProducts();
            RefreshAlerts();
        }
        catch (Exception ex)
        {
            Message = $"Lỗi lưu sản phẩm: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartImportStock(Product? product)
    {
        if (product == null) return;
        ImportingProduct = product;
        ImportCostPrice = product.CostPrice;
        ImportQuantity = 10;
        ImportNotes = "Nhập kho bổ sung";
        IsImportingStock = true;
    }

    [RelayCommand]
    private void ConfirmImportStock()
    {
        if (ImportingProduct == null || ImportQuantity <= 0)
        {
            Message = "Vui lòng nhập số lượng lớn hơn 0!";
            return;
        }

        try
        {
            int userId = _authService.CurrentUser?.UserId ?? 1;
            _inventoryService.ImportStock(ImportingProduct.ProductId, ImportQuantity, ImportCostPrice, userId, ImportNotes ?? "Nhập hàng");
            Message = $"✅ Đã nhập thêm {ImportQuantity} {ImportingProduct.Unit?.UnitName} cho {ImportingProduct.ProductName}!";
            IsImportingStock = false;
            FilterProducts();
            RefreshAlerts();
        }
        catch (Exception ex)
        {
            Message = $"Lỗi nhập kho: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelImportStock()
    {
        IsImportingStock = false;
        ImportingProduct = null;
    }

    [RelayCommand]
    public void OpenCategoryManager()
    {
        var vm = new CategoryManagementViewModel(_inventoryService);
        var dialog = new Views.Dialogs.CategoryManagementDialog(vm)
        {
            Owner = Application.Current?.MainWindow
        };
        dialog.ShowDialog();

        // Tải lại toàn bộ danh mục & đơn vị tính sau khi quản lý
        var currentSelectedCatId = FormSelectedCategory?.CategoryId;
        var currentSelectedUnitId = FormSelectedUnit?.UnitId;

        LoadData();

        if (currentSelectedCatId.HasValue)
        {
            FormSelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == currentSelectedCatId.Value) 
                                ?? Categories.FirstOrDefault(c => c.CategoryId > 0);
        }
        if (currentSelectedUnitId.HasValue)
        {
            FormSelectedUnit = Units.FirstOrDefault(u => u.UnitId == currentSelectedUnitId.Value) 
                            ?? Units.FirstOrDefault();
        }
    }
}
