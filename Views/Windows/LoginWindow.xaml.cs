using System.Windows;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Windows;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        TxtPassword.Password = _viewModel.Password;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.Password))
            {
                if (TxtPassword.Password != _viewModel.Password)
                {
                    TxtPassword.Password = _viewModel.Password;
                }
            }
        };
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TxtUsername.Focus();
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Password != TxtPassword.Password)
        {
            _viewModel.Password = TxtPassword.Password;
        }
    }
}
