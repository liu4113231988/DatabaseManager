using Avalonia.Controls;
using Avalonia.Data;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// 查询结果网格的共享助手：按查询标签列集合重建数据列（主窗口与浮动结果窗口共用）。
/// </summary>
public static class ResultGridHelper
{
    /// <summary>按查询标签的列集合重建指定结果网格的数据列。</summary>
    public static void RebuildColumns(DataGrid grid, QueryTabViewModel tabVm)
    {
        grid.Columns.Clear();

        for (int i = 0; i < tabVm.Columns.Count; i++)
        {
            // 内联编辑模式：非只读列（映射到表的非自增/计算/二进制列）开放双向编辑；否则只读。
            bool editableColumn = tabVm.IsColumnEditable(i);
            bool isPrimaryKey = tabVm.IsPrimaryKeyColumn(i);

            grid.Columns.Add(new DataGridTextColumn
            {
                // 主键列头加 🔑 标识，便于用户识别编辑定位依据。
                Header = isPrimaryKey ? $"🔑 {tabVm.Columns[i]}" : tabVm.Columns[i],
                // 使用 Values[i] 绑定，避免直接索引器路径解析在不同 Avalonia 版本/主题下的兼容性问题
                Binding = new Binding($"Values[{i}]")
                {
                    Mode = editableColumn ? BindingMode.TwoWay : BindingMode.OneWay,
                },
                IsReadOnly = !editableColumn,
                CanUserResize = true,
                Width = DataGridLength.Auto,
                MinWidth = 40,
            });
        }
    }
}
