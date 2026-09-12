using Avalonia.Controls;
using Avalonia.Data;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Views;

public sealed class ResultCompareWindow : Window
{
    public ResultCompareWindow(IReadOnlyList<ResultSnapshot> snapshots)
    {
        Title = "结果快照对照（只读，按显示值比较）"; Width = 1150; Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new global::Avalonia.Thickness(10) };
        for (int side = 0; side < 2; side++)
        {
            var panel = new DockPanel { Margin = new global::Avalonia.Thickness(4) };
            var picker = new ComboBox { ItemsSource = snapshots, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            DockPanel.SetDock(picker, global::Avalonia.Controls.Dock.Top); panel.Children.Add(picker);
            var grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserResizeColumns = true };
            panel.Children.Add(grid);
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedItem is not ResultSnapshot snapshot) return;
                grid.Columns.Clear();
                for (int i = 0; i < snapshot.Result.Columns.Count; i++) grid.Columns.Add(new DataGridTextColumn { Header = snapshot.Result.Columns[i], Binding = new Binding($"Values[{i}]"), IsReadOnly = true });
                grid.ItemsSource = snapshot.Result.Rows.Select(r => new QueryResultRow(snapshot.Result.Columns, r)).ToArray();
            };
            picker.SelectedIndex = Math.Min(side, snapshots.Count - 1);
            Grid.SetColumn(panel, side); layout.Children.Add(panel);
        }
        Content = layout;
    }
}
