using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public event Action? OnLoginSuccess;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private void Login()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập tên đăng nhập và mật khẩu!";
            return;
        }

        IsLoading = true;
        try
        {
            bool success = _authService.Login(Username, Password);
            if (success)
            {
                OnLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác!";
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            System.Windows.MessageBox.Show(
                ex.Message,
                "Không Thể Đăng Nhập - Ca Trực Đang Mở",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi đăng nhập: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
