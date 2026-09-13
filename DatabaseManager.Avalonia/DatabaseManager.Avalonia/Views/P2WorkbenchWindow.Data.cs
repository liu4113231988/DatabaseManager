using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views;

public sealed partial class P2WorkbenchWindow
{
    private Control BuildGeneratorPage()
    {
        var rulesHost = Panel(); var count = Text("100", 85); var seed = Text("42", 85);
        var rules = new List<GenerationRule>(); DataTableInfo? table = null; ConnectionItem? frozenConnection = null;
        IReadOnlyList<DataEditRow>? generated = null; string? signature = null;
        string Signature() => JsonConvert.SerializeObject(rules) + count.Text + ":" + seed.Text;
        return Panel(Label("先选择表并读取字段，再逐列设置规则。枚举用 | 分隔；正则支持字符集、\\d/\\w、{n}/{min,max}。外键引用现有父表候选（每组最多 200 条），请按父表→子表顺序生成。"),
            Row(Button("读取字段规则", async ct =>
            {
                var c = Connection; var t = Table; if (t.IsView) throw new InvalidOperationException("视图不能生成测试数据。");
                var metadata = await _edits.GetTableMetadataAsync(c.Name, c.Database, t.Name, t.Schema, ct);
                if (!metadata.IsSuccess) throw new InvalidOperationException(metadata.ErrorMessage);
                table = metadata.TableInfo; frozenConnection = c; generated = null; rules.Clear(); rulesHost.Children.Clear();
                foreach (var col in table.Columns.Where(c => !c.IsReadOnly))
                {
                    var rule = new GenerationRule { Column = col.Name, Unique = col.IsPrimaryKey && table.PrimaryKeyColumns.Count == 1 }; rules.Add(rule);
                    var kind = Choice("自动", "数值", "文本", "枚举", "正则", "日期", "布尔", "UUID", "二进制"); var min = Text("1", 75); var max = Text("1000000", 90); var values = Text("", 140); var pattern = Text(rule.Pattern, 160); var nulls = Text("0", 60); var unique = new CheckBox { Content = "唯一", IsChecked = rule.Unique };
                    kind.SelectionChanged += (_, _) => rule.Kind = Chosen(kind); min.TextChanged += (_, _) => rule.Minimum = long.TryParse(min.Text, out var n) ? n : long.MaxValue;
                    max.TextChanged += (_, _) => rule.Maximum = long.TryParse(max.Text, out var n) ? n : long.MinValue; values.TextChanged += (_, _) => rule.Values = values.Text ?? "";
                    pattern.TextChanged += (_, _) => rule.Pattern = pattern.Text ?? ""; nulls.TextChanged += (_, _) => rule.NullRatio = double.TryParse(nulls.Text, out var n) ? n : -1;
                    unique.IsCheckedChanged += (_, _) => rule.Unique = unique.IsChecked == true;
                    rulesHost.Children.Add(Panel(Label($"{col.Name} ({col.DataType})"), Row(kind, Label("范围"), min, max, Label("枚举"), values, Label("正则"), pattern, Label("空值比例"), nulls, unique)));
                }
            }), Label("行数"), count, Label("固定种子"), seed), rulesHost,
            Row(Button("生成并预览", async ct =>
            {
                generated = null;
                if (table is null || frozenConnection is null) throw new InvalidOperationException("请先读取字段规则。");
                int rowCount = int.Parse(count.Text!), randomSeed = int.Parse(seed.Text!);
                var foreign = await ForeignKeyValueService.LoadAsync(frozenConnection, table, ct);
                var rows = await Task.Run(() => TestDataGenerator.Generate(table, rules, rowCount, randomSeed, foreign, ct), ct);
                generated = rows; signature = Signature();
                ShowData(new QueryResult { Columns = table.Columns.Select(c => c.Name).ToArray(), Rows = rows.Select(r => (IReadOnlyList<string>)table.Columns.Select(c => r[c.Name] is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : r[c.Name]?.ToString() ?? "NULL").ToArray()).ToArray(), RowCount = rows.Count });
                _output.Text = $"已生成 {rows.Count} 行，目标 {frozenConnection.Name}/{table.DatabaseName}/{table.Schema}.{table.Name}，尚未写入。唯一性检查覆盖本批；与现有数据冲突时由数据库约束回滚整批。";
            }), Button("确认写入预览批次", async ct =>
            {
                if (generated is null || table is null || frozenConnection is null || signature != Signature()) throw new InvalidOperationException("请先按当前规则重新生成预览。");
                if (await AppCore.Common.DialogHelper.ShowConfirmAsync("写入测试数据", $"向 {frozenConnection.Name}/{table.DatabaseName}/{table.Schema}.{table.Name} 插入 {generated.Count} 行？将在一个事务内写入。") != true) return;
                var result = await _edits.SaveChangesAsync(frozenConnection.Name, table.DatabaseName, table.Name, table.Schema, generated, Array.Empty<DataEditRow>(), Array.Empty<DataEditRow>(), ct);
                generated = null;
                if (!result.IsSuccess) throw new InvalidOperationException(result.ErrorMessage);
                _output.Text = $"已写入 {result.RowCount} 行。请重新生成预览后再提交下一批。";
            })));
    }
    private Control BuildQualityPage()
    {
        var limit = Text("10000", 90); var kind = Choice("全部", "空值", "重复值", "异常值（IQR）", "格式不一致"); var column = Text("", 140); QualityReport? report = null;
        void Filter()
        {
            if (report is null) throw new InvalidOperationException("请先剖析表。");
            var findings = report.Findings.Where(f => (Chosen(kind) == "全部" || f.Kind == Chosen(kind)) && (string.IsNullOrEmpty(column.Text) || f.Column == column.Text)).ToArray();
            ShowData(new QueryResult { Columns = new[] { "样本行号", "列", "问题", "值" }, Rows = findings.Select(f => (IReadOnlyList<string>)new[] { f.Row.ToString(), f.Column, f.Kind, f.Value ?? "NULL" }).ToArray(), RowCount = findings.Length });
        }
        return Panel(Label("读取选中表的有限样本，保留 NULL 与空字符串区别。重复值与 IQR 异常提示用于排查，不代表违反业务规则；样本行号不是数据库主键。"),
            Row(Label("最大行数"), limit, Button("剖析选中表", async ct =>
            {
                var sample = await P2TableReader.ReadAsync(Connection, Table.Name, Table.Schema, int.Parse(limit.Text!), ct);
                report = await Task.Run(() => DataQualityProfiler.Analyze(sample.Columns, sample.Rows, sample.IsSample, ct), ct);
                _output.Text = (sample.IsSample ? "采样结果；不代表全表。" : "已读取全表。") + "\n" + string.Join("\n", report.Columns.Select(c => $"{c.Column}：行数 {c.Rows}，NULL {c.Nulls}（{c.NullRate:P2}），重复余数 {c.Duplicates}，最小 {c.Minimum}，最大 {c.Maximum}，格式 {c.Format}\n  前十值分布：{c.Distribution}"));
                Filter();
            })), Row(Label("问题筛选"), kind, Label("列名（空=全部）"), column, Button("筛选明细", _ => { Filter(); return Task.CompletedTask; }),
                Button("导出报告", async ct => { if (report is null) throw new InvalidOperationException("请先剖析。"); var path = await SavePath("quality-report.json"); if (path is not null) await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(report, Formatting.Indented), ct); })));
    }
    private Control BuildMaskPage()
    {
        var col = Text(); var kind = Choice("手机号", "证件", "银行卡", "全部", "自定义"); var pattern = Text("", 220); var replacement = Text("***", 120); var rulesText = new TextBox { IsReadOnly = true, AcceptsReturn = true, Height = 100 };
        var rules = new List<MaskRule>(); QueryResult? masked = null; string? signature = null;
        void Refresh() { rulesText.Text = string.Join("\n", rules.Select(r => $"{r.Column}：{r.Kind}")); masked = null; }
        return Panel(Label("对当前查询结果的独立副本或选中表样本进行脱敏。预览及此处导出均使用脱敏副本，不更新源数据库。短于保留长度的值会全部遮盖。"),
            Row(Button("读取表样本（最多 100000 行）", async ct => { var sample = await P2TableReader.ReadAsync(Connection, Table.Name, Table.Schema, 100000, ct); _sourceResult = sample.ToResult(); masked = null; _output.Text = "可用列：" + string.Join(", ", sample.Columns) + (sample.IsSample ? "\n已达到采样上限，导出也仅包含此样本。" : ""); }),
                Button("显示可用列", _ => { _output.Text = _sourceResult is null ? "没有当前查询结果，请先读取表。" : string.Join(", ", _sourceResult.Columns); return Task.CompletedTask; })),
            Row(Label("列名"), col, kind, Label("自定义正则"), pattern, Label("替换"), replacement, Button("添加 / 更新规则", _ =>
            {
                if (string.IsNullOrWhiteSpace(col.Text)) throw new InvalidOperationException("请填写列名。");
                var rule = new MaskRule { Column = col.Text.Trim(), Kind = Chosen(kind), Pattern = pattern.Text ?? "", Replacement = replacement.Text ?? "" }; DataMasker.Mask("验证规则", rule);
                rules.RemoveAll(r => r.Column.Equals(rule.Column, StringComparison.OrdinalIgnoreCase)); rules.Add(rule); Refresh(); return Task.CompletedTask;
            })), rulesText,
            Row(Button("脱敏预览", _ => { if (_sourceResult is null || rules.Count == 0) throw new InvalidOperationException("请读取结果并添加规则。"); masked = DataMasker.Apply(_sourceResult, rules); signature = JsonConvert.SerializeObject(rules); ShowData(masked); _output.Text = "脱敏副本已生成；源结果和数据库保持原值。"; return Task.CompletedTask; }),
                Button("导出脱敏 CSV", async ct => { if (masked is null || signature != JsonConvert.SerializeObject(rules)) throw new InvalidOperationException("请先重新生成脱敏预览。"); var path = await SavePath("masked-result.csv"); if (path is null) return; await File.WriteAllLinesAsync(path, new[] { ExternalImportSource.Csv(masked.Columns) }.Concat(masked.Rows.Select(ExternalImportSource.Csv)), new System.Text.UTF8Encoding(true), ct); }),
                Button("保存规则", _ => { SaveSettings("mask-rules.json", rules); return Task.CompletedTask; }),
                Button("加载规则", _ => { rules = LoadSettings<List<MaskRule>>("mask-rules.json"); Refresh(); return Task.CompletedTask; }), Button("清空规则", _ => { rules.Clear(); Refresh(); return Task.CompletedTask; })));
    }
}
