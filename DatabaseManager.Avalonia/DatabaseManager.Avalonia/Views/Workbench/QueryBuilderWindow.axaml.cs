using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>查询 / 视图构建：可视化拼装 SELECT，生成结果只进入预览或送入新查询标签。</summary>
public sealed partial class QueryBuilderWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private readonly Action<ConnectionItem, string> _openSql;
    private readonly QueryDesign _design = new();
    private readonly Dictionary<string, BuilderField> _allFields = new();
    private readonly Dictionary<string, BuilderCondition> _conditions = new();

    public QueryBuilderWindow(IServiceProvider services, Action<ConnectionItem, string> openSql, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _openSql = openSql;
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        AggregateBox.ItemsSource = new[] { "", "COUNT", "SUM", "AVG", "MIN", "MAX" }; AggregateBox.SelectedIndex = 0;
        AvailableTables.ItemTemplate = new FuncDataTemplate<ExportTableItem>((t, _) => new TextBlock { Text = t?.DisplayName });
        _shell.ContextCleared += () => AvailableTables.ItemsSource = Array.Empty<ExportTableItem>();
        _shell.TablesLoaded += () => AvailableTables.ItemsSource = _shell.Tables;

        _shell.Bind(AddTableButton, AddTableAsync);
        AddOutputButton.Click += (_, _) => { _design.Fields.Add(Selected() with { Aggregate = Chosen(AggregateBox), Alias = AliasBox.Text ?? "" }); Refresh(); };
        AddOrderButton.Click += (_, _) => _design.OrderBy.Add(Selected() with { Aggregate = Chosen(AggregateBox) });
        AddJoinButton.Click += (_, _) =>
        {
            var left = _allFields[Chosen(LeftBox)]; var right = _allFields[Chosen(RightBox)];
            _design.Joins.Add(new(Chosen(JoinTypeBox), left.TableAlias, left.Column, right.TableAlias, right.Column)); Refresh();
        };
        AddConditionButton.Click += (_, _) =>
        {
            var field = Selected(); string group = GroupIdBox.Text?.Trim() ?? "1";
            if (!_conditions.TryGetValue(group, out var condition)) _conditions[group] = condition = new BuilderCondition();
            condition.Logic = Chosen(GroupLogicBox);
            condition.Children.Add(new() { TableAlias = field.TableAlias, Column = field.Column, Operator = Chosen(OperatorBox), Value = ValueBox.Text ?? "" });
            Refresh();
        };
        UndoOutputButton.Click += (_, _) => { if (_design.Fields.Count > 0) _design.Fields.RemoveAt(_design.Fields.Count - 1); Refresh(); };
        UndoJoinButton.Click += (_, _) => { if (_design.Joins.Count > 0) _design.Joins.RemoveAt(_design.Joins.Count - 1); Refresh(); };
        ClearConditionsButton.Click += (_, _) => { _conditions.Clear(); _design.Where = null; Refresh(); };
        ClearOrderButton.Click += (_, _) => _design.OrderBy.Clear();
        ClearDesignButton.Click += (_, _) => { _design.Tables.Clear(); _design.Fields.Clear(); _design.Joins.Clear(); _design.OrderBy.Clear(); _design.Where = null; _conditions.Clear(); _allFields.Clear(); Refresh(); };
        PreviewSqlButton.Click += (_, _) => _shell.SetOutput(Build());
        SendSqlButton.Click += (_, _) => { var connection = _shell.Connection; var sql = Build(); _openSql(connection, sql); _shell.SetStatus("已打开查询标签，尚未执行。"); };

        _shell.Bind(SaveDesignButton, async ct =>
        {
            _ = Build();
            var path = await _shell.SavePath("query-design.json");
            if (path is not null) await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(_design, Formatting.Indented), ct);
        });
        _shell.Bind(LoadDesignButton, async ct =>
        {
            var path = await _shell.OpenPath("加载查询设计"); if (path is null) return;
            if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("设计文件过大。");
            var loaded = JsonConvert.DeserializeObject<QueryDesign>(await File.ReadAllTextAsync(path, ct)) ?? throw new ArgumentException("设计无效。");
            _ = VisualQueryBuilder.Build(loaded, ConnectionHelper.ParseDatabaseType(_shell.Connection.DatabaseType));
            var connection = _shell.Connection;
            _design.Tables.Clear(); _design.Tables.AddRange(loaded.Tables);
            _design.Fields.Clear(); _design.Fields.AddRange(loaded.Fields);
            _design.Joins.Clear(); _design.Joins.AddRange(loaded.Joins);
            _design.OrderBy.Clear(); _design.OrderBy.AddRange(loaded.OrderBy);
            _design.Where = loaded.Where; _design.Descending = loaded.Descending;
            _conditions.Clear(); if (loaded.Where is not null) _conditions["加载的条件"] = loaded.Where;
            _allFields.Clear();
            foreach (var t in _design.Tables)
                foreach (var col in await _shell.Files.GetTableColumnsAsync(connection, t.Name, t.Schema, ct))
                    _allFields[t.Alias + "." + col] = new(t.Alias, col);
            Refresh();
        });

        // 从左侧表列表拖拽到“已加入的表”区域即可加入
        DragDrop.SetAllowDrop(DropArea, true);
        DragDrop.SetAllowDrop(SelectedTables, true);
        AvailableTables.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(AvailableTables).Properties.IsLeftButtonPressed && AvailableTables.SelectedItem is ExportTableItem source)
            {
                var transfer = new DataTransfer();
                transfer.Add(DataTransferItem.CreateText(source.DisplayName));
                AppExceptionHandler.Run(
                    () => DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy),
                    "拖拽查询对象");
            }
        }, RoutingStrategies.Bubble, true);
        DragDrop.AddDragOverHandler(DropArea, (_, e) => e.DragEffects = DragDropEffects.Copy);
        DragDrop.AddDropHandler(DropArea, (_, _) => AddTableButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

        _shell.Initialize();
        Refresh();
    }

    private BuilderField Selected() => FieldBox.SelectedItem is string name && _allFields.TryGetValue(name, out var field)
        ? field : throw new InvalidOperationException("请选择字段。");

    private void Refresh()
    {
        SelectedTables.ItemsSource = _design.Tables.Select(t => (string.IsNullOrEmpty(t.Schema) ? "" : t.Schema + ".") + $"{t.Name} AS {t.Alias}").ToArray();
        FieldBox.ItemsSource = _allFields.Keys.ToArray(); LeftBox.ItemsSource = _allFields.Keys.ToArray(); RightBox.ItemsSource = _allFields.Keys.ToArray();
        FieldBox.SelectedIndex = _allFields.Count > 0 ? 0 : -1; LeftBox.SelectedIndex = FieldBox.SelectedIndex; RightBox.SelectedIndex = FieldBox.SelectedIndex;
        SummaryBox.Text = "输出：" + string.Join(", ", _design.Fields.Select(f => f.Aggregate + "(" + f.TableAlias + "." + f.Column + ")"))
            + "\n关联：" + string.Join("; ", _design.Joins.Select(j => $"{j.LeftAlias}.{j.LeftColumn} = {j.RightAlias}.{j.RightColumn}"))
            + "\n条件组：" + string.Join(", ", _conditions.Select(c => c.Key + "（" + c.Value.Children.Count + "）"));
    }

    private string Build()
    {
        if (_conditions.Count > 0) _design.Where = new BuilderCondition { Logic = Chosen(RootLogicBox), Children = _conditions.Values.ToList() };
        _design.Descending = DescendingBox.IsChecked == true;
        return VisualQueryBuilder.Build(_design, ConnectionHelper.ParseDatabaseType(_shell.Connection.DatabaseType), ViewNameBox.Text, ViewSchemaBox.Text);
    }

    private async Task AddTableAsync(CancellationToken ct)
    {
        var table = AvailableTables.SelectedItem as ExportTableItem ?? _shell.Table;
        var columns = await _shell.Files.GetTableColumnsAsync(_shell.Connection, table.Name, table.Schema, ct);
        string alias = "t" + (_design.Tables.Count + 1);
        _design.Tables.Add(new BuilderTable(table.Name, table.Schema, alias));
        foreach (var column in columns) _allFields[alias + "." + column] = new BuilderField(alias, column);
        Refresh();
    }

    private static string Chosen(ComboBox combo) => combo.SelectedItem?.ToString() ?? "";
}
