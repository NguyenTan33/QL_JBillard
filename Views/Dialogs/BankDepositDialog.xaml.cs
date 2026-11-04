using System.Windows;
using QL_JBillard.ViewModels;

namespace QL_JBillard.Views.Dialogs;

public partial class BankDepositDialog : Window
{
    public BankDepositDialog(BankDepositDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => Close();
    }
}
