using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class OpenTableDialogViewModel : ObservableObject
{
    private readonly ITableService _tableService;
    private readonly ICustomerService? _customerService;
    private readonly IAuthService _authService;

    public TableItemViewModel TableItem { get; }

    [ObservableProperty]
    private string _playMode = "HOURLY"; // "HOURLY" or "COMBO"

    public bool IsHourlyMode => PlayMode == "HOURLY";
    public bool IsComboMode => PlayMode == "COMBO";

    [ObservableProperty]
    private ObservableCollection<PlayCombo> _availableCombos = new();

    [ObservableProperty]
    private PlayCombo? _selectedCombo;

    [ObservableProperty]
    private string _customerPhone = string.Empty;

    [ObservableProperty]
    private Customer? _customer;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action? RequestClose;
    public bool DialogResult { get; private set; }

    public OpenTableDialogViewModel(
        TableItemViewModel tableItem,
        ITableService tableService,
        IAuthService authService,
        ICustomerService? customerService = null)
    {
        TableItem = tableItem;
        _tableService = tableService;
        _authService = authService;
        _customerService = customerService;

        LoadCombos();
    }

    private void LoadCombos()
    {
        try
        {
            var combos = _tableService.GetCombos(TableItem.TableTypeId, activeOnly: true);
            AvailableCombos = new ObservableCollection<PlayCombo>(combos);
            SelectedCombo = AvailableCombos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi tải danh sách combo: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetMode(string mode)
    {
        PlayMode = mode;
        OnPropertyChanged(nameof(IsHourlyMode));
        OnPropertyChanged(nameof(IsComboMode));
    }

    [RelayCommand]
    private void SearchCustomer()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(CustomerPhone))
        {
            Customer = null;
            return;
        }

        try
        {
            Customer = _customerService?.FindByPhone(CustomerPhone.Trim());
            if (Customer == null)
            {
                ErrorMessage = "Không tìm thấy hội viên với số điện thoại này.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi tra cứu: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ConfirmOpen()
    {
        ErrorMessage = null;
        try
        {
            int userId = _authService.CurrentUser?.UserId ?? 1;
            int? comboId = (PlayMode == "COMBO" && SelectedCombo != null) ? SelectedCombo.ComboId : null;

            _tableService.OpenTable(
                TableItem.TableId,
                userId,
                Customer?.CustomerId,
                Notes,
                PlayMode,
                comboId
            );

            DialogResult = true;
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}
