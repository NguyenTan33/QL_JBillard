using System.Windows;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Dialogs;

public partial class OrderDialog : Window
{
    public OrderDialog(OrderDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
