using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>数据脱敏：对查询结果副本或表样本按规则脱敏，预览后导出 CSV。</summary>
public sealed partial class MaskWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private readonly List<MaskRule> _rules = new();
    private QueryResult? _sourceResult;
    private QueryResult? _masked;
    private string? _signature;

    public MaskWindow(IServiceProvider services, string? connectionName = null, string? database = null, QueryResult? result = null)
    {
        InitializeComponent();
        _sourceResult = result;
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        _shell.Bind(ReadSampleButton, async ct =>
        {
            var table = _shell.Table;
            var sample = await P2TableReader.ReadAsync(_shell.Connection, table.Name, table.Schema, 100000, ct);
            _sourceResult = sample.ToResult(); _masked = null;
            _shell.SetOutput("可用列：" + string.Join(", ", sample.Columns) + (sample.IsSample ? "\n已达到采样上限，导出也仅包含此样本。" : ""));
        });
        _shell.Bind(ShowColumnsButton, _ =>
        {
            _shell.SetOutput(_sourceResult is null ? "没有当前查询结果，请先读取表。" : string.Join(", ", _sourceResult.Columns));
            return Task.CompletedTask;
        });
        _shell.Bind(AddRuleButton, _ =>
        {
            if (string.IsNullOrWhiteSpace(ColumnBox.Text)) throw new InvalidOperationException("请填写列名。");
            var rule = new MaskRule
            {
                Column = ColumnBox.Text.Trim(), Kind = KindBox.SelectedItem?.ToString() ?? "手机号",
                Pattern = PatternBox.Text ?? "", Replacement = ReplacementBox.Text ?? ""
            };
            DataMasker.Mask("验证规则", rule);
            _rules.RemoveAll(r => r.Column.Equals(rule.Column, StringComparison.OrdinalIgnoreCase)); _rules.Add(rule); Refresh();
            return Task.CompletedTask;
        });
        _shell.Bind(PreviewButton, _ =>
        {
            if (_sourceResult is null || _rules.Count == 0) throw new InvalidOperationException("请读取结果并添加规则。");
            _masked = DataMasker.Apply(_sourceResult, _rules); _signature = JsonConvert.SerializeObject(_rules);
            _shell.ShowData(_masked); _shell.SetOutput("脱敏副本已生成；源结果和数据库保持原值。");
            return Task.CompletedTask;
        });
        _shell.Bind(ExportCsvButton, async ct =>
        {
            if (_masked is null || _signature != JsonConvert.SerializeObject(_rules)) throw new InvalidOperationException("请先重新生成脱敏预览。");
            var path = await _shell.SavePath("masked-result.csv"); if (path is null) return;
            await File.WriteAllLinesAsync(path, new[] { ExternalImportSource.Csv(_masked.Columns) }.Concat(_masked.Rows.Select(ExternalImportSource.Csv)), new System.Text.UTF8Encoding(true), ct);
        });
        _shell.Bind(SaveRulesButton, _ => { WorkbenchSupport.SaveSettings("mask-rules.json", _rules); return Task.CompletedTask; });
        _shell.Bind(LoadRulesButton, _ =>
        {
            _rules.Clear(); _rules.AddRange(WorkbenchSupport.LoadSettings<List<MaskRule>>("mask-rules.json")); Refresh();
            return Task.CompletedTask;
        });
        _shell.Bind(ClearRulesButton, _ => { _rules.Clear(); Refresh(); return Task.CompletedTask; });
        _shell.Initialize();
    }

    private void Refresh()
    {
        RulesText.Text = string.Join("\n", _rules.Select(r => $"{r.Column}：{r.Kind}"));
        _masked = null;
    }
}
