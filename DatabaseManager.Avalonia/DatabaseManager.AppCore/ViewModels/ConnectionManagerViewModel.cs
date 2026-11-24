using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 连接管理 ViewModel（阶段 1）。
/// 提供连接列表展示、增删改查、连接测试、名称唯一性校验等能力。
/// UI 无关，可独立单测。
/// </summary>
public partial class ConnectionManagerViewModel : ViewModelBase
{
    private readonly IDbConnectionService _connectionService;
    private int _refreshVersion;

    public ObservableCollection<ConnectionItem> Connections { get; } = new();

    public IReadOnlyList<string> DatabaseTypes { get; }

    [ObservableProperty]
    private string _selectedDatabaseType = string.Empty;

    [ObservableProperty]
    private ConnectionItem? _selectedConnection;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ConnectionManagerViewModel(IDbConnectionService connectionService)
    {
        _connectionService = connectionService;

        DatabaseTypes = DbInterpreterHelper.GetDisplayDatabaseTypes()
            .Select(t => t.ToString())
            .ToList();

        if (DatabaseTypes.Count > 0)
        {
            SelectedDatabaseType = DatabaseTypes[0];
        }
    }

    /// <summary>刷新连接列表（按当前选中的数据库类型）。</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        int version = ++_refreshVersion;
        var databaseType = SelectedDatabaseType;
        IsBusy = true;
        StatusMessage = "正在读取连接...";

        try
        {
            var items = await _connectionService.GetConnectionsAsync(cancellationToken);
            if (version != _refreshVersion || cancellationToken.IsCancellationRequested) return;

            var selectedId = SelectedConnection?.Id;
            Connections.Clear();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(databaseType) || item.DatabaseType == databaseType)
                    Connections.Add(item);
            }
            SelectedConnection = string.IsNullOrEmpty(selectedId)
                ? null : Connections.FirstOrDefault(item => item.Id == selectedId);
            StatusMessage = $"{Connections.Count} 条连接";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (version == _refreshVersion && !cancellationToken.IsCancellationRequested)
                StatusMessage = $"读取连接失败：{ex.Message}";
        }
        finally
        {
            if (version == _refreshVersion) IsBusy = false;
        }
    }

    /// <summary>新增连接：构造一个空连接项并选中，由视图打开编辑。</summary>
    public ConnectionItem CreateNew()
    {
        var item = ConnectionItem.New(SelectedDatabaseType);
        item.Name = string.Empty;
        SelectedConnection = item;
        return item;
    }

    /// <summary>连接测试：返回可用的数据库列表。</summary>
    public async Task<IReadOnlyList<string>> TestConnectionAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            return await _connectionService.TestConnectionAsync(connection, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>保存连接配置。</summary>
    public async Task<bool> SaveAsync(ConnectionItem connection, CancellationToken cancellationToken = default)
    {
        var id = await _connectionService.SaveAsync(connection, cancellationToken);
        return !string.IsNullOrEmpty(id);
    }

    /// <summary>校验连接名称是否已存在。</summary>
    public async Task<bool> IsNameExistedAsync(bool isAdd, string? accountId, string name, string? id, CancellationToken cancellationToken = default)
        => await _connectionService.IsNameExistedAsync(isAdd, accountId, name, id, cancellationToken);

    /// <summary>删除选中连接。</summary>
    public async Task<bool> DeleteAsync(IEnumerable<ConnectionItem> items, CancellationToken cancellationToken = default)
    {
        var ids = items.Where(i => !string.IsNullOrEmpty(i.Id)).Select(i => i.Id!).ToList();
        if (ids.Count == 0)
            return false;

        var result = await _connectionService.DeleteAsync(ids, cancellationToken);
        if (result)
        {
            foreach (var item in items)
            {
                Connections.Remove(item);
            }
        }

        return result;
    }
}
