using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// 查询结果网格的共享助手：按查询标签列集合重建数据列（主窗口与浮动结果窗口共用）。
/// 使用 FuncDataTemplate 直接读写 QueryResultRow，避免 AOT 下 Binding(string) 的反射路径。
/// </summary>
public static class ResultGridHelper
{
    /// <summary>按查询标签的列集合重建指定结果网格的数据列。</summary>
    public static void RebuildColumns(DataGrid grid, QueryTabViewModel tabVm)
    {
        grid.Columns.Clear();

        for (int i = 0; i < tabVm.Columns.Count; i++)
        {
            int column = i;
            bool editableColumn = tabVm.IsColumnEditable(i);
            bool isPrimaryKey = tabVm.IsPrimaryKeyColumn(i);
            string header = isPrimaryKey ? $"🔑 {tabVm.Columns[i]}" : tabVm.Columns[i];

            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = header,
                IsReadOnly = !editableColumn,
                CanUserResize = true,
                Width = DataGridLength.Auto,
                MinWidth = 40,
                CellTemplate = editableColumn
                    ? new FuncDataTemplate<QueryResultRow>((row, _) => CreateEditor(row, column))
                    : new FuncDataTemplate<QueryResultRow>((row, _) => new TextBlock { Text = row?[column] }),
            });
        }
    }

    private static TextBox CreateEditor(QueryResultRow? row, int column)
    {
        var box = new TextBox
        {
            Text = row?[column] ?? string.Empty,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
        };

        void Commit()
        {
            if (row is null) return;
            string value = box.Text ?? string.Empty;
            if (!string.Equals(row[column], value, StringComparison.Ordinal))
            {
                row.SetValue(column, value);
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter)
            {
                Commit();
            }
        };
        return box;
    }
}
