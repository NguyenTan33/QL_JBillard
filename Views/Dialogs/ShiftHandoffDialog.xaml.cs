using System.Collections.Generic;
using System.Windows;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Views.Dialogs;

public enum ShiftHandoffChoice
{
    Cancel,
    HandoffToNextShift,
    CheckoutAllFirst
}

public partial class ShiftHandoffDialog : Window
{
    public ShiftHandoffChoice Choice { get; private set; } = ShiftHandoffChoice.Cancel;

    public ShiftHandoffDialog(IEnumerable<Table> activeTables)
    {
        InitializeComponent();
        ActiveTablesList.ItemsSource = activeTables;
    }

    private void Handoff_Click(object sender, RoutedEventArgs e)
    {
        Choice = ShiftHandoffChoice.HandoffToNextShift;
        DialogResult = true;
        Close();
    }

    private void CheckoutAll_Click(object sender, RoutedEventArgs e)
    {
        Choice = ShiftHandoffChoice.CheckoutAllFirst;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = ShiftHandoffChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
