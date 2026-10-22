using System.Windows;
using System.Windows.Input;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Dialogs;

public partial class CheckoutDialog : Window
{
    private readonly CheckoutDialogViewModel _viewModel;

    public CheckoutDialog(CheckoutDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.F9 || (e.Key == Key.Enter && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox)))
        {
            if (_viewModel.ConfirmCheckoutCommand.CanExecute(null))
            {
                _viewModel.ConfirmCheckoutCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
