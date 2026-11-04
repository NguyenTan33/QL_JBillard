using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class UserManagementViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private ObservableCollection<User> _users = new();

    [ObservableProperty]
    private User? _selectedUser;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    // Form Binding
    [ObservableProperty]
    private string _formUsername = string.Empty;

    [ObservableProperty]
    private string _formPassword = string.Empty;

    [ObservableProperty]
    private string _formFullName = string.Empty;

    [ObservableProperty]
    private string? _formPhone;

    [ObservableProperty]
    private UserRole _formRole = UserRole.Cashier;

    [ObservableProperty]
    private bool _formIsActive = true;

    [ObservableProperty]
    private bool _isEditing = false;

    [ObservableProperty]
    private string? _message;

    public ObservableCollection<UserRole> AvailableRoles { get; } = new()
    {
        UserRole.Cashier,
        UserRole.Admin
    };

    public UserManagementViewModel(IAuthService authService)
    {
        _authService = authService;
        LoadUsers();
    }

    [RelayCommand]
    public void LoadUsers()
    {
        var all = _authService.GetAllUsers();
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            all = all.Where(u => u.Username.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                 u.FullName.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ||
                                 (u.PhoneNumber != null && u.PhoneNumber.Contains(SearchKeyword)))
                     .ToList();
        }
        Users = new ObservableCollection<User>(all);
    }

    partial void OnSearchKeywordChanged(string value) => LoadUsers();

    partial void OnSelectedUserChanged(User? value)
    {
        if (value != null)
        {
            IsEditing = true;
            FormUsername = value.Username;
            FormFullName = value.FullName;
            FormPhone = value.PhoneNumber;
            FormRole = value.Role;
            FormIsActive = value.IsActive;
            FormPassword = string.Empty; // Để trống nếu không muốn đổi mật khẩu
            Message = null;
        }
    }

    [RelayCommand]
    private void NewUser()
    {
        SelectedUser = null;
        IsEditing = false;
        FormUsername = string.Empty;
        FormPassword = string.Empty;
        FormFullName = string.Empty;
        FormPhone = string.Empty;
        FormRole = UserRole.Cashier;
        FormIsActive = true;
        Message = null;
    }

    [RelayCommand]
    private void SaveUser()
    {
        if (string.IsNullOrWhiteSpace(FormUsername) || string.IsNullOrWhiteSpace(FormFullName))
        {
            Message = "⚠️ Vui lòng nhập đầy đủ Tên đăng nhập và Họ tên!";
            return;
        }

        if (!IsEditing)
        {
            // Thêm mới
            if (string.IsNullOrWhiteSpace(FormPassword))
            {
                Message = "⚠️ Vui lòng nhập mật khẩu cho tài khoản mới!";
                return;
            }

            bool success = _authService.CreateUser(FormUsername, FormPassword, FormFullName, FormPhone, FormRole);
            if (success)
            {
                Message = $"✅ Tạo tài khoản {FormUsername} ({FormRole}) thành công!";
                NewUser();
                LoadUsers();
            }
            else
            {
                Message = $"❌ Tên đăng nhập '{FormUsername}' đã tồn tại trong hệ thống!";
            }
        }
        else
        {
            // Cập nhật
            if (SelectedUser == null) return;

            bool success = _authService.UpdateUser(
                SelectedUser.UserId,
                FormFullName,
                FormPhone,
                FormRole,
                FormIsActive,
                string.IsNullOrWhiteSpace(FormPassword) ? null : FormPassword
            );

            if (success)
            {
                Message = $"✅ Cập nhật thông tin tài khoản {SelectedUser.Username} thành công!";
                LoadUsers();
            }
            else
            {
                Message = "❌ Không thể cập nhật thông tin tài khoản!";
            }
        }
    }

    [RelayCommand]
    private void ToggleStatus(User? user)
    {
        var target = user ?? SelectedUser;
        if (target == null) return;

        bool success = _authService.ToggleUserStatus(target.UserId);
        if (success)
        {
            string status = target.IsActive ? "ĐÃ MỞ KHÓA" : "ĐÃ KHÓA";
            Message = $"✅ Tài khoản {target.Username} {status} thành công!";
            LoadUsers();
        }
        else
        {
            Message = "⚠️ Không thể thay đổi trạng thái của chính tài khoản đang đăng nhập!";
        }
    }

    [RelayCommand]
    private void DeleteUser(User? user)
    {
        var target = user ?? SelectedUser;
        if (target == null) return;

        bool success = _authService.DeleteUser(target.UserId);
        if (success)
        {
            Message = $"✅ Đã xử lý xóa/vô hiệu hóa tài khoản {target.Username}!";
            NewUser();
            LoadUsers();
        }
        else
        {
            Message = "⚠️ Không thể xóa tài khoản Quản trị viên chính hoặc tài khoản đang đăng nhập!";
        }
    }
}
