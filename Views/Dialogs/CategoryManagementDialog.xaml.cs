using System.Windows;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Dialogs;

public partial class CategoryManagementDialog : Window
{
    public CategoryManagementDialog(CategoryManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
