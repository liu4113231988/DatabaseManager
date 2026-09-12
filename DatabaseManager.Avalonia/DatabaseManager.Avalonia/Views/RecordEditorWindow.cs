using Avalonia.Controls;
using Avalonia.Layout;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

public sealed class RecordEditorWindow : Window
{
    public RecordEditorWindow(QueryTabViewModel tab, QueryResultRow row, ConnectionItem? connection)
    {
        Title = "单记录编辑"; Width = 720; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Spacing = 8, Margin = new global::Avalonia.Thickness(16) };
        var boxes = new List<TextBox>();
        for (int i = 0; i < tab.Columns.Count; i++)
        {
            int index = i;
            panel.Children.Add(new TextBlock { Text = tab.Columns[i] });
            var box = new TextBox { Text = row[i], AcceptsReturn = true, MinHeight = 45, MaxHeight = 160, IsReadOnly = !tab.IsColumnEditable(i) };
            boxes.Add(box); panel.Children.Add(box);
            var column = tab.EditableTable?.Columns.FirstOrDefault(c => c.Name.Equals(tab.SourceColumnName(tab.Columns[index]), StringComparison.OrdinalIgnoreCase));
            if (column is not null && DatabaseInterpreter.Utility.DataTypeHelper.IsBinaryType(column.DataType))
            {
                panel.Children.Add(new TextBlock { Text = "二进制值使用 0x 开头的十六进制；可从文件导入（最多 1 MiB）。" });
                var button = new Button { Content = "从文件读取二进制", IsEnabled = !box.IsReadOnly };
                button.Click += async (_, _) =>
                {
                    var files = await StorageProvider.OpenFilePickerAsync(new() { AllowMultiple = false });
                    if (files.Count == 0) return;
                    try
                    {
                        await using var stream = await files[0].OpenReadAsync();
                        using var buffer = new MemoryStream();
                        var bytes = new byte[8192];
                        int read;
                        while ((read = await stream.ReadAsync(bytes)) > 0)
                        {
                            if (buffer.Length + read > 1024 * 1024) throw new InvalidOperationException("文件超过 1 MiB。");
                            buffer.Write(bytes, 0, read);
                        }
                        box.Text = "0x" + Convert.ToHexString(buffer.ToArray());
                    }
                    catch (Exception ex) { Title = ex.Message; }
                };
                panel.Children.Add(button);
            }
        }
        var foreign = new StackPanel { Spacing = 6 };
        var load = new Button { Content = "加载外键候选值（每个关系最多 200 行）", IsEnabled = connection is not null && tab.EditableTable is not null };
        panel.Children.Add(load); panel.Children.Add(foreign);
        var cts = new CancellationTokenSource();
        Closed += (_, _) => { cts.Cancel(); cts.Dispose(); };
        load.Click += async (_, _) =>
        {
            load.IsEnabled = false;
            try
            {
                var choices = await ForeignKeyValueService.LoadAsync(connection!, tab.EditableTable!, cts.Token);
                foreign.Children.Clear();
                foreach (var choice in choices)
                {
                    foreign.Children.Add(new TextBlock { Text = choice.Key.Name + " → " + choice.Key.ReferencedTableName });
                    var picker = new ComboBox { ItemsSource = choice.Values.Rows.Select(r => string.Join(" | ", r)).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch };
                    picker.SelectionChanged += (_, _) =>
                    {
                        if (picker.SelectedIndex < 0) return;
                        var values = choice.Values.Rows[picker.SelectedIndex];
                        for (int k = 0; k < choice.Key.Columns.Count; k++)
                            for (int c = 0; c < tab.Columns.Count; c++)
                                if (!boxes[c].IsReadOnly && tab.SourceColumnName(tab.Columns[c]).Equals(choice.Key.Columns[k].ColumnName, StringComparison.OrdinalIgnoreCase)) boxes[c].Text = values[k];
                    };
                    foreign.Children.Add(picker);
                }
            }
            catch (Exception ex) { foreign.Children.Add(new TextBlock { Text = ex.Message }); }
            finally { load.IsEnabled = true; }
        };
        var apply = new Button { Content = "应用到网格（随后点击保存写入数据库）" };
        apply.Click += (_, _) =>
        {
            try
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    var column = tab.EditableTable?.Columns.FirstOrDefault(c => c.Name == tab.SourceColumnName(tab.Columns[i]));
                    if (column is not null && DatabaseInterpreter.Utility.DataTypeHelper.IsBinaryType(column.DataType) && !string.IsNullOrEmpty(boxes[i].Text))
                    {
                        var text = boxes[i].Text!;
                        _ = Convert.FromHexString(text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text);
                    }
                }
                tab.ApplyFormValues(row, boxes.Select(b => b.Text).ToArray()); Close();
            }
            catch (Exception ex) { Title = "输入无效：" + ex.Message; }
        };
        panel.Children.Add(apply);
        Content = new ScrollViewer { Content = panel };
    }
}
