using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>数据质量剖析：读取有限样本并输出空值、重复值与异常值提示，可筛选明细并导出报告。</summary>
public sealed partial class QualityWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private QualityReport? _report;

    public QualityWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        _shell.Bind(ProfileButton, ProfileAsync);
        _shell.Bind(FilterButton, _ => { Filter(); return Task.CompletedTask; });
        _shell.Bind(ExportReportButton, async ct =>
        {
            if (_report is null) throw new InvalidOperationException("请先剖析。");
            var path = await _shell.SavePath("quality-report.json");
            if (path is not null) await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(_report, Formatting.Indented), ct);
        });
        _shell.Initialize();
    }

    private void Filter()
    {
        if (_report is null) throw new InvalidOperationException("请先剖析表。");
        string kind = FilterKindBox.SelectedItem?.ToString() ?? "全部";
        string column = FilterColumnBox.Text ?? "";
        var findings = _report.Findings.Where(f => (kind == "全部" || f.Kind == kind) && (string.IsNullOrEmpty(column) || f.Column == column)).ToArray();
        _shell.ShowData(new QueryResult
        {
            Columns = new[] { "样本行号", "列", "问题", "值" },
            Rows = findings.Select(f => (IReadOnlyList<string>)new[] { f.Row.ToString(), f.Column, f.Kind, f.Value ?? "NULL" }).ToArray(),
            RowCount = findings.Length
        });
    }

    private async Task ProfileAsync(CancellationToken ct)
    {
        var table = _shell.Table;
        var sample = await P2TableReader.ReadAsync(_shell.Connection, table.Name, table.Schema, int.Parse(LimitBox.Text!), ct);
        _report = await Task.Run(() => DataQualityProfiler.Analyze(sample.Columns, sample.Rows, sample.IsSample, ct), ct);
        _shell.SetOutput((sample.IsSample ? "采样结果；不代表全表。" : "已读取全表。") + "\n"
            + string.Join("\n", _report.Columns.Select(c => $"{c.Column}：行数 {c.Rows}，NULL {c.Nulls}（{c.NullRate:P2}），重复余数 {c.Duplicates}，最小 {c.Minimum}，最大 {c.Maximum}，格式 {c.Format}\n  前十值分布：{c.Distribution}")));
        Filter();
    }
}
