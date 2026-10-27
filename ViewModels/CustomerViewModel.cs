using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class CustomerViewModel : ObservableObject
{
    private readonly ICustomerService _customerService;
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    [ObservableProperty]
    private ObservableCollection<Customer> _customers = new();

    [ObservableProperty]
    private ObservableCollection<MembershipTier> _tiers = new();

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    // Form
    [ObservableProperty]
    private string _formPhone = string.Empty;

    [ObservableProperty]
    private string _formFullName = string.Empty;

    [ObservableProperty]
    private string? _formNotes;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string _formTitle = "ĐĂNG KÝ THÀNH VIÊN MỚI";

    [ObservableProperty]
    private bool _isEditingCustomer = false;

    [ObservableProperty]
    private int _editingCustomerId = 0;

    public bool IsAdmin => _authService.CurrentUser?.Role == UserRole.Admin;

    public CustomerViewModel(
        ICustomerService customerService,
        IAuthService authService,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _customerService = customerService;
        _authService = authService;
        _contextFactory = contextFactory;
        LoadData();
    }

    [RelayCommand]
    public void LoadData()
    {
        OnPropertyChanged(nameof(IsAdmin));
        var tiers = _customerService.GetAllTiers();
        Tiers = new ObservableCollection<MembershipTier>(tiers);
        SearchCustomers();
    }

    partial void OnSearchKeywordChanged(string value) => SearchCustomers();

    private void SearchCustomers()
    {
        var list = _customerService.GetAllCustomers(SearchKeyword);
        Customers = new ObservableCollection<Customer>(list);
    }

    [RelayCommand]
    public void EditCustomer(Customer? customer)
    {
        if (customer == null) return;
        if (!IsAdmin)
        {
            MessageBox.Show("Chỉ Quản trị viên (Admin) mới có quyền chỉnh sửa thông tin hội viên!", 
                "Không Có Quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsEditingCustomer = true;
        EditingCustomerId = customer.CustomerId;
        FormPhone = customer.PhoneNumber;
        FormFullName = customer.FullName;
        FormNotes = customer.Notes;
        FormTitle = $"CẬP NHẬT THÔNG TIN: {customer.FullName.ToUpper()}";
        Message = null;
    }

    [RelayCommand]
    public void CancelEditCustomer()
    {
        IsEditingCustomer = false;
        EditingCustomerId = 0;
        FormPhone = string.Empty;
        FormFullName = string.Empty;
        FormNotes = null;
        FormTitle = "ĐĂNG KÝ THÀNH VIÊN MỚI";
        Message = null;
    }

    [RelayCommand]
    public void DeleteCustomer(Customer? customer)
    {
        if (customer == null) return;
        if (!IsAdmin)
        {
            MessageBox.Show("Chỉ Quản trị viên (Admin) mới có quyền xóa hoặc khóa hội viên!", 
                "Không Có Quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var context = _contextFactory.CreateDbContext();
        int invCount = context.Invoices.Count(i => i.CustomerId == customer.CustomerId);
        int sessionCount = context.TableSessions.Count(s => s.CustomerId == customer.CustomerId);

        if (invCount > 0 || sessionCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa hội viên '{customer.FullName}' ({customer.PhoneNumber})!\n\nHội viên này đã có lịch sử tham gia {sessionCount} phiên chơi và {invCount} hóa đơn thanh toán trong hệ thống.\nVui lòng giữ lại để đảm bảo tính toàn vẹn số liệu và báo cáo tài chính.",
                "Cảnh Báo Tham Chiếu Khóa Ngoại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Message = $"❌ Không thể xóa hội viên '{customer.FullName}' vì đã có dữ liệu hóa đơn/phiên chơi!";
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa hội viên '{customer.FullName}' ({customer.PhoneNumber}) khỏi hệ thống không?",
            "Xác Nhận Xóa Hội Viên",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var dbCust = context.Customers.Find(customer.CustomerId);
            if (dbCust != null)
            {
                context.Customers.Remove(dbCust);
                context.SaveChanges();
            }

            Message = $"✅ Đã xóa hội viên '{customer.FullName}' thành công!";
            if (EditingCustomerId == customer.CustomerId)
            {
                CancelEditCustomer();
            }
            SearchCustomers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xóa hội viên do có dữ liệu liên kết tham chiếu: {ex.Message}", 
                "Lỗi Khóa Ngoại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void SaveCustomer()
    {
        string phone = FormPhone.Trim();
        string fullName = FormFullName.Trim();

        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(fullName))
        {
            Message = "⚠️ Vui lòng nhập số điện thoại và họ tên!";
            return;
        }

        // Kiểm tra quyền đối với Thu ngân: không được sửa thông tin hội viên đã có
        var existing = _customerService.FindByPhone(phone);
        if (!IsAdmin)
        {
            if (existing != null)
            {
                Message = $"⚠️ Số điện thoại {phone} đã là hội viên ({existing.FullName})! Thu ngân chỉ được thêm hội viên mới, không có quyền sửa.";
                MessageBox.Show(
                    $"Số điện thoại {phone} đã đăng ký hội viên cho '{existing.FullName}'!\n\nThu ngân chỉ có quyền đăng ký hội viên mới hoặc tra cứu. Chỉ Quản trị viên (Admin) mới có quyền chỉnh sửa thông tin hội viên.",
                    "Không Có Quyền Chỉnh Sửa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }
        else
        {
            // Admin đang ở chế độ thêm mới nhưng gõ trúng SĐT đã có
            if (!IsEditingCustomer && existing != null)
            {
                var ask = MessageBox.Show(
                    $"Số điện thoại {phone} đã thuộc về hội viên '{existing.FullName}'!\n\nBạn có muốn cập nhật thông tin cho hội viên này không?",
                    "Hội Viên Đã Tồn Tại",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ask != MessageBoxResult.Yes) return;
            }
        }

        try
        {
            var customer = _customerService.CreateOrUpdateCustomer(phone, fullName, FormNotes);
            Message = IsEditingCustomer 
                ? $"✅ Đã cập nhật thông tin hội viên {customer.FullName} thành công!" 
                : $"✅ Đã thêm mới hội viên {customer.FullName} thành công!";
            
            CancelEditCustomer();
            SearchCustomers();
        }
        catch (Exception ex)
        {
            Message = $"❌ Lỗi lưu hội viên: {ex.Message}";
        }
    }
}
