using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Services;
using Newtonsoft.Json;

namespace DatabaseManager.Avalonia.Views;

public sealed partial class P2WorkbenchWindow
{
    private readonly ListBox _builderAvailable = new() { Height = 110, MinWidth = 210 };
    private Control BuildQueryPage()
    {
        var design = new QueryDesign(); var selectedTables = new ListBox { Height = 100, MinWidth = 280 };
        var allFields = new Dictionary<string, BuilderField>(); var field = new ComboBox { MinWidth = 220 };
        var left = new ComboBox { MinWidth = 200 }; var right = new ComboBox { MinWidth = 200 };
        var aggregate = Choice("", "COUNT", "SUM", "AVG", "MIN", "MAX"); var alias = Text("", 120);
        var joinType = Choice("INNER", "LEFT"); var conditionOp = Choice("=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL"); var value = Text();
        var groupId = Text("1", 60); var groupLogic = Choice("AND", "OR"); var rootLogic = Choice("AND", "OR");
        var conditions = new Dictionary<string, BuilderCondition>();
        var viewName = Text("", 150); var viewSchema = Text("", 100); var descending = new CheckBox { Content = "降序" };
        var summary = new TextBox { IsReadOnly = true, AcceptsReturn = true, Height = 100 };
        BuilderField Selected() => field.SelectedItem is string s && allFields.TryGetValue(s, out var f) ? f : throw new InvalidOperationException("请选择字段。");
        void Refresh()
        {
            selectedTables.ItemsSource = design.Tables.Select(t => (string.IsNullOrEmpty(t.Schema) ? "" : t.Schema + ".") + $"{t.Name} AS {t.Alias}").ToArray();
            field.ItemsSource = allFields.Keys.ToArray(); left.ItemsSource = allFields.Keys.ToArray(); right.ItemsSource = allFields.Keys.ToArray();
            field.SelectedIndex = allFields.Count > 0 ? 0 : -1; left.SelectedIndex = field.SelectedIndex; right.SelectedIndex = field.SelectedIndex;
            summary.Text = "输出：" + string.Join(", ", design.Fields.Select(f => f.Aggregate + "(" + f.TableAlias + "." + f.Column + ")")) + "\n关联：" + string.Join("; ", design.Joins.Select(j => $"{j.LeftAlias}.{j.LeftColumn} = {j.RightAlias}.{j.RightColumn}")) + "\n条件组：" + string.Join(", ", conditions.Select(c => c.Key + "（" + c.Value.Children.Count + "）"));
        }
        string Build()
        {
            if (conditions.Count > 0) design.Where = new BuilderCondition { Logic = Chosen(rootLogic), Children = conditions.Values.ToList() };
            design.Descending = descending.IsChecked == true;
            return VisualQueryBuilder.Build(design, ConnectionHelper.ParseDatabaseType(Connection.DatabaseType), viewName.Text, viewSchema.Text);
        }
        async Task AddTable(CancellationToken ct)
        {
            var t = _builderAvailable.SelectedItem as ExportTableItem ?? Table;
            var cols = await _files.GetTableColumnsAsync(Connection, t.Name, t.Schema, ct);
            string tableAlias = "t" + (design.Tables.Count + 1); design.Tables.Add(new(t.Name, t.Schema, tableAlias));
            foreach (var col in cols) allFields[tableAlias + "." + col] = new(tableAlias, col);
            Refresh();
        }
        var add = Button("加入表", AddTable);
        var drop = Panel(Label("已加入的表（可将左侧表拖入此区域）"), selectedTables, add);
        ExportTableItem? dragging = null;
        _builderAvailable.ItemTemplate = new FuncDataTemplate<ExportTableItem>((t, _) => new TextBlock { Text = t?.DisplayName });
        _builderAvailable.AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(_builderAvailable).Properties.IsLeftButtonPressed) dragging = _builderAvailable.SelectedItem as ExportTableItem; }, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (dragging is not null && new Rect(drop.Bounds.Size).Contains(e.GetPosition(drop))) { _builderAvailable.SelectedItem = dragging; add.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent)); }
            dragging = null;
        }, RoutingStrategies.Tunnel);
        return Panel(Label("选择或拖入表，配置关联与字段；生成结果只进入预览，不自动执行。"), Row(_builderAvailable, drop),
            Row(Label("字段"), field, aggregate, Label("输出别名"), alias,
                Button("添加输出", _ => { design.Fields.Add(Selected() with { Aggregate = Chosen(aggregate), Alias = alias.Text ?? "" }); Refresh(); return Task.CompletedTask; }),
                Button("添加排序", _ => { design.OrderBy.Add(Selected() with { Aggregate = Chosen(aggregate) }); return Task.CompletedTask; }), descending),
            Row(Label("关联"), left, joinType, right, Button("添加关联", _ => { var l = allFields[Chosen(left)]; var r = allFields[Chosen(right)]; design.Joins.Add(new(Chosen(joinType), l.TableAlias, l.Column, r.TableAlias, r.Column)); Refresh(); return Task.CompletedTask; })),
            Row(Label("字段条件"), conditionOp, value, Label("组号"), groupId, Label("组内"), groupLogic, Label("组间"), rootLogic,
                Button("添加条件", _ => { var f = Selected(); string group = groupId.Text?.Trim() ?? "1"; if (!conditions.TryGetValue(group, out var c)) conditions[group] = c = new BuilderCondition(); c.Logic = Chosen(groupLogic); c.Children.Add(new() { TableAlias = f.TableAlias, Column = f.Column, Operator = Chosen(conditionOp), Value = value.Text ?? "" }); Refresh(); return Task.CompletedTask; })),
            summary,
            Row(Button("撤销上个输出", _ => { if (design.Fields.Count > 0) design.Fields.RemoveAt(design.Fields.Count - 1); Refresh(); return Task.CompletedTask; }), Button("撤销上个关联", _ => { if (design.Joins.Count > 0) design.Joins.RemoveAt(design.Joins.Count - 1); Refresh(); return Task.CompletedTask; }), Button("清空条件", _ => { conditions.Clear(); design.Where = null; Refresh(); return Task.CompletedTask; }), Button("清空排序", _ => { design.OrderBy.Clear(); return Task.CompletedTask; })),
            Row(Label("可选：创建视图"), viewSchema, viewName, Button("预览 SQL", _ => { _output.Text = Build(); return Task.CompletedTask; }), Button("送入新查询标签", _ => { var c = Connection; var sql = Build(); _openSql(c, sql); _status.Text = "已打开查询标签，尚未执行。"; return Task.CompletedTask; }),
                Button("清空设计", _ => { design = new(); conditions.Clear(); allFields.Clear(); Refresh(); return Task.CompletedTask; }),
                Button("保存设计", async ct => { _ = Build(); var path = await SavePath("query-design.json"); if (path is not null) await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(design, Formatting.Indented), ct); }),
                Button("加载设计", async ct => { var path = await OpenPath("加载查询设计"); if (path is null) return; if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("设计文件过大。"); var loaded = JsonConvert.DeserializeObject<QueryDesign>(await File.ReadAllTextAsync(path, ct)) ?? throw new ArgumentException("设计无效。"); _ = VisualQueryBuilder.Build(loaded, ConnectionHelper.ParseDatabaseType(Connection.DatabaseType)); design = loaded; conditions.Clear(); if (design.Where is not null) conditions["加载的条件"] = design.Where; allFields.Clear(); foreach (var t in design.Tables) foreach (var col in await _files.GetTableColumnsAsync(Connection, t.Name, t.Schema, ct)) allFields[t.Alias + "." + col] = new(t.Alias, col); Refresh(); })));
    }
}
