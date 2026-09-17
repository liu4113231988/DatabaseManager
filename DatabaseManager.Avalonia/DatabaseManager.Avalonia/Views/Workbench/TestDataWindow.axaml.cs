using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;
using static DatabaseManager.Avalonia.Views.Workbench.WorkbenchBuilders;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>测试数据生成：按表结构逐列设置规则，先生成预览，确认后再写入。</summary>
public sealed partial class TestDataWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private readonly List<GenerationRule> _rules = new();
    private DataTableInfo? _table;
    private ConnectionItem? _frozenConnection;
    private IReadOnlyList<DataEditRow>? _generated;
    private string? _signature;

    public TestDataWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageHost,
            InitialConnection = connectionName, InitialDatabase = database
        };

        _shell.Bind(ReadColumnsButton, ReadColumnsAsync);
        _shell.Bind(GenerateButton, GenerateAsync);
        _shell.Bind(WriteButton, WriteAsync);
        // 本页由内部列规则区自行滚动，整页高度固定占视口 75% 并再让出 100px 给下方日志预览区，不产生第二个滚动条
        _shell.UseFixedPageRatio(0.75, 100);
        _shell.Initialize();
    }

    private string Signature() => JsonConvert.SerializeObject(_rules) + RowCountBox.Text + ":" + SeedBox.Text;

    private async Task ReadColumnsAsync(CancellationToken ct)
    {
        var connection = _shell.Connection; var table = _shell.Table;
        if (table.IsView) throw new InvalidOperationException("视图不能生成测试数据。");
        var metadata = await _shell.Edits.GetTableMetadataAsync(connection.Name, connection.Database, table.Name, table.Schema, ct);
        if (!metadata.IsSuccess) throw new InvalidOperationException(metadata.ErrorMessage);
        _table = metadata.TableInfo; _frozenConnection = connection; _generated = null; _rules.Clear(); RulesHost.Children.Clear();
        foreach (var column in _table.Columns.Where(c => !c.IsReadOnly))
        {
            var rule = new GenerationRule { Column = column.Name, Unique = column.IsPrimaryKey && _table.PrimaryKeyColumns.Count == 1 };
            _rules.Add(rule);
            var kind = Choice("自动", "数值", "文本", "枚举", "正则", "日期", "布尔", "UUID", "二进制");
            var min = Text("1", 75); var max = Text("1000000", 90); var values = Text("", 140);
            var pattern = Text(rule.Pattern, 160); var nulls = Text("0", 60);
            var unique = new CheckBox { Content = "唯一", IsChecked = rule.Unique };
            kind.SelectionChanged += (_, _) => rule.Kind = Chosen(kind);
            min.TextChanged += (_, _) => rule.Minimum = long.TryParse(min.Text, out var n) ? n : long.MaxValue;
            max.TextChanged += (_, _) => rule.Maximum = long.TryParse(max.Text, out var n) ? n : long.MinValue;
            values.TextChanged += (_, _) => rule.Values = values.Text ?? "";
            pattern.TextChanged += (_, _) => rule.Pattern = pattern.Text ?? "";
            nulls.TextChanged += (_, _) => rule.NullRatio = double.TryParse(nulls.Text, out var n) ? n : -1;
            unique.IsCheckedChanged += (_, _) => rule.Unique = unique.IsChecked == true;
            RulesHost.Children.Add(Panel(Label($"{column.Name} ({column.DataType})"),
                Row(kind, Label("范围"), min, max, Label("枚举"), values, Label("正则"), pattern, Label("空值比例"), nulls, unique)));
        }
    }

    private async Task GenerateAsync(CancellationToken ct)
    {
        _generated = null;
        if (_table is null || _frozenConnection is null) throw new InvalidOperationException("请先读取字段规则。");
        int rowCount = int.Parse(RowCountBox.Text!), seed = int.Parse(SeedBox.Text!);
        var foreign = await ForeignKeyValueService.LoadAsync(_frozenConnection, _table, ct);
        var rows = await Task.Run(() => TestDataGenerator.Generate(_table, _rules, rowCount, seed, foreign, ct), ct);
        _generated = rows; _signature = Signature();
        _shell.ShowData(new QueryResult
        {
            Columns = _table.Columns.Select(c => c.Name).ToArray(),
            Rows = rows.Select(r => (IReadOnlyList<string>)_table.Columns.Select(c => r[c.Name] is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : r[c.Name]?.ToString() ?? "NULL").ToArray()).ToArray(),
            RowCount = rows.Count
        });
        _shell.SetOutput($"已生成 {rows.Count} 行，目标 {_frozenConnection.Name}/{_table.DatabaseName}/{_table.Schema}.{_table.Name}，尚未写入。唯一性检查覆盖本批；与现有数据冲突时由数据库约束回滚整批。");
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        if (_generated is null || _table is null || _frozenConnection is null || _signature != Signature())
            throw new InvalidOperationException("请先按当前规则重新生成预览。");
        if (await AppCore.Common.DialogHelper.ShowConfirmAsync("写入测试数据",
            $"向 {_frozenConnection.Name}/{_table.DatabaseName}/{_table.Schema}.{_table.Name} 插入 {_generated.Count} 行？将在一个事务内写入。") != true) return;
        var result = await _shell.Edits.SaveChangesAsync(_frozenConnection.Name, _table.DatabaseName, _table.Name, _table.Schema,
            _generated, Array.Empty<DataEditRow>(), Array.Empty<DataEditRow>(), ct);
        _generated = null;
        if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
        _shell.SetOutput($"已写入 {result.RowCount} 行。请重新生成预览后再提交下一批。");
    }
}
