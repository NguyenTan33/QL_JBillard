using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.ViewModels;

public partial class SwitchTableViewModel : ObservableObject
{
    private readonly ITableService _tableService;
    private readonly IAuthService _authService;

    public TableItemViewModel FromTable { get; }

    [ObservableProperty]
    private ObservableCollection<Table> _availableTables = new();

    [ObservableProperty]
    private Table? _selectedTargetTable;

    [ObservableProperty]
    private string? _reason = "Khách yêu cầu đổi bàn";

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasAvailableTables => AvailableTables != null && AvailableTables.Count > 0;

    public event Action? RequestClose;
    public event Action? OnSwitchSuccess;

    public SwitchTableViewModel(ITableService tableService, IAuthService authService, TableItemViewModel fromTable)
    {
        _tableService = tableService;
        _authService = authService;
        FromTable = fromTable;

        LoadAvailableTables();
    }

    private void LoadAvailableTables()
    {
        var tables = _tableService.GetAllTables()
                                  .Where(t => t.TableId != FromTable.TableId && t.Status == TableStatus.Available)
                                  .ToList();
        AvailableTables = new ObservableCollection<Table>(tables);
        SelectedTargetTable = AvailableTables.FirstOrDefault();
        OnPropertyChanged(nameof(HasAvailableTables));
    }

    [RelayCommand]
    private void ConfirmSwitch()
    {
        if (SelectedTargetTable == null)
        {
            ErrorMessage = "Vui lòng chọn bàn đích muốn chuyển sang!";
            return;
        }

        try
        {
            int userId = _authService.CurrentUser?.UserId ?? 1;
            bool success = _tableService.SwitchTable(FromTable.TableId, SelectedTargetTable.TableId, userId, Reason);
            if (success)
            {
                OnSwitchSuccess?.Invoke();
                RequestClose?.Invoke();
            }
            else
            {
                ErrorMessage = "Chuyển bàn thất bại! Vui lòng kiểm tra trạng thái bàn đích.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lỗi: {ex.Message}";
        }
    }
}
