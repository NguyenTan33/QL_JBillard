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

    // --- Quản lý Danh Mục ---
    [ObservableProperty]
    private string _categoryInputName = string.Empty;

    [ObservableProperty]
    private bool _isEditingCategory = false;

    [ObservableProperty]
    private ProductCategory? _editingCategory;

    // --- Quản lý Đơn Vị Tính ---
    [ObservableProperty]
    private string _unitInputName = string.Empty;

    [ObservableProperty]
    private bool _isEditingUnit = false;

    [ObservableProperty]
    private Unit? _editingUnit;

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

        CancelEditCategory();
        CancelEditUnit();
        Message = null;
    }

    // ==========================================
    // CÁC THAO TÁC DANH MỤC: THÊM / SỬA / XÓA
    // ==========================================

    [RelayCommand]
    public void StartEditCategory(ProductCategory? category)
    {
        if (category == null) return;
        EditingCategory = category;
        CategoryInputName = category.CategoryName;
        IsEditingCategory = true;
        Message = $"✎ Đang sửa nhóm danh mục: '{category.CategoryName}' (Mã: #{category.CategoryId})";
    }

    [RelayCommand]
    public void CancelEditCategory()
    {
        IsEditingCategory = false;
        EditingCategory = null;
        CategoryInputName = string.Empty;
    }

    [RelayCommand]
    public void SaveCategory()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(CategoryInputName))
        {
            Message = "⚠️ Vui lòng nhập tên nhóm danh mục!";
            return;
        }

        string trimmed = CategoryInputName.Trim();

        if (IsEditingCategory && EditingCategory != null)
        {
            // Kiểm tra trùng tên với nhóm khác
            if (CategoriesList.Any(c => c.CategoryId != EditingCategory.CategoryId && 
                                        c.CategoryName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                Message = $"⚠️ Nhóm danh mục '{trimmed}' đã tồn tại ở mã khác!";
                return;
            }

            EditingCategory.CategoryName = trimmed;
            if (_inventoryService.SaveCategory(EditingCategory))
            {
                Message = $"✅ Đã cập nhật thành công tên nhóm danh mục thành '{trimmed}'!";
                CancelEditCategory();
                var cats = _inventoryService.GetAllCategories();
                CategoriesList = new ObservableCollection<ProductCategory>(cats);
            }
            else
            {
                Message = "❌ Không thể cập nhật nhóm danh mục!";
            }
        }
        else
        {
            // Thêm mới
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
                CategoryInputName = string.Empty;
                var cats = _inventoryService.GetAllCategories();
                CategoriesList = new ObservableCollection<ProductCategory>(cats);
            }
            else
            {
                Message = "❌ Không thể lưu danh mục mới!";
            }
        }
    }

    [RelayCommand]
    public void DeleteCategory(ProductCategory? category)
    {
        if (category == null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa nhóm danh mục '{category.CategoryName}' (Mã: #{category.CategoryId}) không?",
            "Xác Nhận Xóa Danh Mục",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_inventoryService.DeleteCategory(category.CategoryId, out string error))
        {
            Message = $"✅ Đã xóa nhóm danh mục '{category.CategoryName}'!";
            if (EditingCategory?.CategoryId == category.CategoryId)
            {
                CancelEditCategory();
            }
            var cats = _inventoryService.GetAllCategories();
            CategoriesList = new ObservableCollection<ProductCategory>(cats);
        }
        else
        {
            MessageBox.Show(error, "Không Thể Xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            Message = $"⚠️ {error}";
        }
    }

    // ==========================================
    // CÁC THAO TÁC ĐƠN VỊ TÍNH: THÊM / SỬA / XÓA
    // ==========================================

    [RelayCommand]
    public void StartEditUnit(Unit? unit)
    {
        if (unit == null) return;
        EditingUnit = unit;
        UnitInputName = unit.UnitName;
        IsEditingUnit = true;
        Message = $"✎ Đang sửa đơn vị tính: '{unit.UnitName}' (Mã: #{unit.UnitId})";
    }

    [RelayCommand]
    public void CancelEditUnit()
    {
        IsEditingUnit = false;
        EditingUnit = null;
        UnitInputName = string.Empty;
    }

    [RelayCommand]
    public void SaveUnit()
    {
        Message = null;
        if (string.IsNullOrWhiteSpace(UnitInputName))
        {
            Message = "⚠️ Vui lòng nhập tên đơn vị tính!";
            return;
        }

        string trimmed = UnitInputName.Trim();

        if (IsEditingUnit && EditingUnit != null)
        {
            if (UnitsList.Any(u => u.UnitId != EditingUnit.UnitId && 
                                   u.UnitName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                Message = $"⚠️ Đơn vị tính '{trimmed}' đã tồn tại ở mã khác!";
                return;
            }

            EditingUnit.UnitName = trimmed;
            if (_inventoryService.SaveUnit(EditingUnit))
            {
                Message = $"✅ Đã cập nhật thành công đơn vị tính thành '{trimmed}'!";
                CancelEditUnit();
                var units = _inventoryService.GetAllUnits();
                UnitsList = new ObservableCollection<Unit>(units);
            }
            else
            {
                Message = "❌ Không thể cập nhật đơn vị tính!";
            }
        }
        else
        {
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
                UnitInputName = string.Empty;
                var units = _inventoryService.GetAllUnits();
                UnitsList = new ObservableCollection<Unit>(units);
            }
            else
            {
                Message = "❌ Không thể lưu đơn vị tính mới!";
            }
        }
    }

    [RelayCommand]
    public void DeleteUnit(Unit? unit)
    {
        if (unit == null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa đơn vị tính '{unit.UnitName}' (Mã: #{unit.UnitId}) không?",
            "Xác Nhận Xóa Đơn Vị Tính",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        if (_inventoryService.DeleteUnit(unit.UnitId, out string error))
        {
            Message = $"✅ Đã xóa đơn vị tính '{unit.UnitName}'!";
            if (EditingUnit?.UnitId == unit.UnitId)
            {
                CancelEditUnit();
            }
            var units = _inventoryService.GetAllUnits();
            UnitsList = new ObservableCollection<Unit>(units);
        }
        else
        {
            MessageBox.Show(error, "Không Thể Xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            Message = $"⚠️ {error}";
        }
    }
}
