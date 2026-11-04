using System.Windows;
using System.Windows.Input;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Dialogs;

public partial class OpenTableDialog : Window
{
    private readonly OpenTableDialogViewModel _viewModel;

    public OpenTableDialog(OpenTableDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = viewModel.DialogResult;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && tb.AcceptsReturn))
        {
            if (_viewModel.ConfirmOpenCommand.CanExecute(null))
            {
                _viewModel.ConfirmOpenCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
