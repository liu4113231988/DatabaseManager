using Avalonia.Controls;
using Avalonia.Input.Platform;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views;

public sealed partial class P2WorkbenchWindow
{
    private Control BuildDictionaryPage()
    {
        var title = Text("数据字典", 260); var intro = Text("", 440); var objects = Text("", 440);
        var objectTemplate = Text("{schema}.{name} — {comment}", 650); var columnTemplate = Text("{name} | {type} | 可空:{nullable} | 默认:{default} | {comment}", 650);
        var format = Choice("PDF", "HTML"); DictionaryDocument? document = null;
        DictionaryOptions Options() => new() { Title = title.Text ?? "数据字典", Introduction = intro.Text ?? "", ObjectTemplate = objectTemplate.Text ?? "", ColumnTemplate = columnTemplate.Text ?? "", Objects = (objects.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() };
        return Panel(Label("生成对象目录、列属性和外键关系说明。可选对象用逗号分隔（schema.table）；空表示全部表与视图。模板是纯文本占位符，导出 HTML 时会转义。"),
            Row(Label("标题"), title, Label("简介"), intro), Row(Label("对象"), objects, Button("加入选中对象", _ => { var names = Options().Objects; if (!names.Contains(Table.DisplayName)) names.Add(Table.DisplayName); objects.Text = string.Join(",", names); return Task.CompletedTask; })),
            Row(Label("对象模板"), objectTemplate), Row(Label("列模板"), columnTemplate),
            Row(Button("生成预览", async ct => { document = await DataDictionaryService.ReadAsync(Connection, Options(), ct); _output.Text = string.Join("\n", document.Lines); }), format,
                Button("导出文档", async ct => { if (document is null) throw new InvalidOperationException("请先生成预览。"); var path = await SavePath("data-dictionary." + Chosen(format).ToLowerInvariant()); if (path is not null) await DataDictionaryService.SaveAsync(document, path, ct); }),
                Button("保存模板", _ => { SaveSettings("dictionary-template.json", Options()); return Task.CompletedTask; }),
                Button("加载模板", _ => { var o = LoadSettings<DictionaryOptions>("dictionary-template.json"); title.Text = o.Title; intro.Text = o.Introduction; objectTemplate.Text = o.ObjectTemplate; columnTemplate.Text = o.ColumnTemplate; objects.Text = string.Join(",", o.Objects); document = null; return Task.CompletedTask; })),
            Row(Button("创建每日文档计划", async ct =>
            {
                var path = await SavePath("data-dictionary." + Chosen(format).ToLowerInvariant()); if (path is null) return;
                var connection = Connection; var schedule = new ScheduleDefinition { Name = "数据字典 " + connection.Database, ConnectionName = connection.Name, DatabaseName = connection.Database, TaskType = ScheduleTaskTypes.Documentation, ExportFilePath = path, Dictionary = Options(), Enabled = false };
                _services.GetRequiredService<IScheduleService>().Save(schedule); _output.Text = "已创建停用状态的每日 02:00 文档计划。请在任务定时调度中核对输出路径、时间并启用；运行失败会记录在任务中心。";
            })));
    }
    private Control BuildAiPage()
    {
        var endpoint = Text("http://localhost:11434/v1", 370); var model = Text("", 180); var keyEnv = Text("DBM_AI_API_KEY", 210);
        var mode = Choice("生成 SQL", "解释 SQL", "纠错", "优化建议"); var request = new TextBox { AcceptsReturn = true, Height = 80, Watermark = "说明需求，包含数据库方言及预期行为" };
        var sql = new TextBox { AcceptsReturn = true, Height = 75, Watermark = "要解释、纠错或优化的 SQL（可留空）" };
        var schemaText = new TextBox { IsReadOnly = true, AcceptsReturn = true, Height = 80 }; var selected = new Dictionary<string, string>();
        var consent = new CheckBox { Content = "允许将下方 SQL、请求和已选元数据发送到上方接口" };
        AiSqlSettings Settings() => new() { Endpoint = endpoint.Text?.Trim() ?? "", Model = model.Text?.Trim() ?? "", ApiKeyEnvironmentVariable = keyEnv.Text?.Trim() ?? "" };
        return Panel(Label("支持 OpenAI 兼容聊天接口与本地 Ollama。API 密钥从环境变量读取；设置文件不保存密钥。不会发送行数据或连接密码，AI 返回内容只供预览。"),
            Row(Label("接口"), endpoint, Label("模型"), model), Row(Label("密钥环境变量"), keyEnv, mode,
                Button("保存设置", _ => { SaveSettings("ai-sql.json", Settings()); return Task.CompletedTask; }), Button("加载设置", _ => { var s = LoadSettings<AiSqlSettings>("ai-sql.json"); endpoint.Text = s.Endpoint; model.Text = s.Model; keyEnv.Text = s.ApiKeyEnvironmentVariable; consent.IsChecked = false; return Task.CompletedTask; })),
            Row(Button("加入选中表元数据", async ct => { var c = Connection; var t = Table; var meta = await _edits.GetTableMetadataAsync(c.Name, c.Database, t.Name, t.Schema, ct); if (!meta.IsSuccess) throw new InvalidOperationException(meta.ErrorMessage); selected[t.DisplayName] = t.DisplayName + " (" + string.Join(", ", meta.TableInfo.Columns.Select(x => x.Name + " " + x.DataType)) + ")"; schemaText.Text = "方言：" + c.DatabaseType + "\n" + string.Join("\n", selected.Values); consent.IsChecked = false; }),
                Button("清空元数据", _ => { selected.Clear(); schemaText.Text = ""; consent.IsChecked = false; return Task.CompletedTask; })), schemaText, request, sql, consent,
            Row(Button("发送并预览回答", async ct => { if (consent.IsChecked != true) throw new InvalidOperationException("请先确认允许发送所选内容。"); _output.Text = await AiSqlAssistant.AskAsync(Settings(), Chosen(mode), request.Text ?? "", sql.Text ?? "", schemaText.Text ?? "", ct); }),
                Button("复制回答", async _ => { if (Clipboard is not null) await Clipboard.SetTextAsync(_output.Text ?? ""); })));
    }
    private Control BuildTransferPage()
    {
        var list = new ListBox { Height = 170, SelectionMode = SelectionMode.Multiple };
        list.ItemTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<ConnectionItem>((c, _) => new TextBlock { Text = c?.Description });
        list.ItemsSource = _connections.GetConnections();
        var include = new CheckBox { Content = "导出时携带数据库 / SSH 密码（必须加密）" }; var password = Text("", 240); password.PasswordChar = '●';
        var policy = Choice("重名自动改名", "跳过重名"); bool isImport = false;
        return Panel(Label("按 Ctrl/Shift 多选连接。导入先预览再保存；原连接不会被覆盖。携带密码时使用独立口令保护文件，请在接收端输入相同口令。"), list, Row(include, Label("保护口令"), password),
            Row(Button("加载本机连接", _ => { list.ItemsSource = _connections.GetConnections(); isImport = false; return Task.CompletedTask; }),
                Button("导出所选连接", async ct => { var selected = list.SelectedItems?.Cast<ConnectionItem>().ToArray() ?? Array.Empty<ConnectionItem>(); var json = ConnectionTransferService.Export(selected, include.IsChecked == true, password.Text ?? ""); var path = await SavePath("connections.dbm.json"); if (path is not null) await File.WriteAllTextAsync(path, json, ct); }),
                Button("打开文件并预览导入", async ct => { var path = await OpenPath("选择连接配置文件"); if (path is null) return; if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidOperationException("连接文件过大。"); var imported = ConnectionTransferService.Preview(await File.ReadAllTextAsync(path, ct), password.Text ?? ""); list.ItemsSource = imported; isImport = true; _output.Text = $"已解读 {imported.Count} 个连接。请选择需要导入的项，再点击保存。"; }), policy,
                Button("保存所选导入连接", async ct => { if (!isImport) throw new InvalidOperationException("请先打开导入文件。"); var selected = list.SelectedItems?.Cast<ConnectionItem>().ToArray() ?? Array.Empty<ConnectionItem>(); if (selected.Length == 0) throw new InvalidOperationException("请选择至少一个连接。"); int count = await ConnectionTransferService.ImportAsync(_connections, selected, policy.SelectedIndex == 0, ct); isImport = false; _output.Text = $"已导入 {count} 个连接。"; _connection.ItemsSource = _connections.GetConnections(); })));
    }
    private Control BuildExternalPage()
    {
        var type = Choice("ODBC", "Access", "DBF"); var source = Text("", 570); var driver = Text("Microsoft Access Driver (*.mdb, *.accdb)", 380); var encoding = Text("GB18030", 140);
        var select = new TextBox { Text = "SELECT * FROM table_name ORDER BY id", AcceptsReturn = true, Height = 80 }; string? snapshot = null;
        return Panel(Label("ODBC 需要安装与本程序位数一致的驱动。Access 使用指定的 ODBC 驱动；DBF 支持 dBASE III 的字符、数值、日期和逻辑列。先生成稳定 CSV 快照，再使用原导入向导做列映射、错误行检查和按行数续导。NULL/空值沿用 CSV 导入语义。"),
            Row(type, Label("ODBC 连接串 / 文件路径"), source, Button("选择源文件", async _ => { var path = await OpenPath("选择 Access / DBF 文件"); if (path is not null) source.Text = path; })),
            Row(Label("Access 驱动"), driver, Label("DBF 编码"), encoding), Label("ODBC / Access 源查询（单条 SELECT；建议按唯一键排序）"), select,
            Row(Button("生成导入快照", async ct =>
            {
                var path = await SavePath("import-snapshot.csv"); if (path is null) return;
                if (File.Exists(source.Text) && string.Equals(Path.GetFullPath(source.Text!), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("快照不能覆盖源文件。");
                snapshot = null;
                int rows = type.SelectedIndex == 2 ? await ExternalImportSource.SnapshotDbfAsync(source.Text!, path, encoding.Text!, ct) : await ExternalImportSource.SnapshotOdbcAsync(type.SelectedIndex == 1 ? ExternalImportSource.AccessConnectionString(source.Text!, driver.Text!) : source.Text!, select.Text!, path, ct);
                snapshot = path; _output.Text = $"快照生成完成：{rows} 行\n{path}\n续导时使用同一快照，不要重新抓取变化中的源数据。";
            }), Button("打开已有快照", async _ => { snapshot = await OpenPath("选择已有 CSV 快照"); }),
                Button("进入列映射与导入", async _ => { if (snapshot is null || !File.Exists(snapshot)) throw new InvalidOperationException("请先生成或选择快照。"); var vm = _services.GetRequiredService<ImportViewModel>(); vm.SetFilePath(snapshot); await new ImportWindow(vm).ShowDialog<object?>(this); })));
    }
}
