using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class CategoryManagementViewModel : ObservableObject
{
    private readonly IInventoryService _inventoryService;

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categoriesList = new();

    [ObservableProperty]
    private ObservableCollection<Unit> _unitsList = new();

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string _newUnitName = string.Empty;

    [ObservableProperty]
    private ProductCategory? _editingCategory;

    [ObservableProperty]
    private string _editingCategoryName = string.Empty;

    [ObservableProperty]
    private string? _message;

    public CategoryManagementViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        var cats = _inventoryService.GetAllCategories();
        CategoriesList = new ObservableCollection<ProductCategory>(cats);

        var units = _inventoryService.GetAllUnits();
        UnitsList = new ObservableCollection<Unit>(units);

        NewCategoryName = string.Empty;
        NewUnitName = string.Empty;
        EditingCategory = null;
        EditingCategoryName = string.Empty;
        Message = null;
    }

    [RelayCommand]
    public void AddCategory()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            Message = "⚠️ Vui lòng nhập tên nhóm danh mục mới!";
            return;
        }

        string trimmed = NewCategoryName.Trim();
        if (CategoriesList.Any(c => c.CategoryName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"⚠️ Nhóm danh mục '{trimmed}' đã tồn tại!";
            return;
        }

        var cat = new ProductCategory
        {
            CategoryName = trimmed,
            IsActive = true
        };

        if (_inventoryService.SaveCategory(cat))
        {
            Message = $"✅ Đã thêm nhóm danh mục '{trimmed}' thành công!";
            NewCategoryName = string.Empty;
            LoadData();
        }
        else
        {
            Message = "❌ Không thể lưu danh mục mới!";
        }
    }

    [RelayCommand]
    public void DeleteCategory(ProductCategory? category)
    {
        if (category == null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa nhóm danh mục '{category.CategoryName}' không?",
            "Xác Nhận Xóa Danh Mục",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_inventoryService.DeleteCategory(category.CategoryId, out string error))
        {
            Message = $"✅ Đã xóa nhóm danh mục '{category.CategoryName}'!";
            LoadData();
        }
        else
        {
            MessageBox.Show(error, "Không Thể Xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            Message = $"⚠️ {error}";
        }
    }

    [RelayCommand]
    public void AddUnit()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(NewUnitName))
        {
            Message = "⚠️ Vui lòng nhập tên đơn vị tính mới (VD: Bao, Điếu, Thùng, Khay)!";
            return;
        }

        string trimmed = NewUnitName.Trim();
        if (UnitsList.Any(u => u.UnitName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"⚠️ Đơn vị tính '{trimmed}' đã có sẵn!";
            return;
        }

        var unit = new Unit
        {
            UnitName = trimmed
        };

        if (_inventoryService.SaveUnit(unit))
        {
            Message = $"✅ Đã thêm đơn vị tính '{trimmed}' thành công!";
            NewUnitName = string.Empty;
            LoadData();
        }
        else
        {
            Message = "❌ Không thể lưu đơn vị tính mới!";
        }
    }

    [RelayCommand]
    public void DeleteUnit(Unit? unit)
    {
        if (unit == null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa đơn vị tính '{unit.UnitName}' không?",
            "Xác Nhận Xóa Đơn Vị Tính",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_inventoryService.DeleteUnit(unit.UnitId, out string error))
        {
            Message = $"✅ Đã xóa đơn vị tính '{unit.UnitName}'!";
            LoadData();
        }
        else
        {
            MessageBox.Show(error, "Không Thể Xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            Message = $"⚠️ {error}";
        }
    }
}
