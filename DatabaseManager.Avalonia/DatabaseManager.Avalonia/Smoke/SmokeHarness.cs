using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Common;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using DatabaseManager.Avalonia.Controls;
using DatabaseManager.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia.Smoke;

/// <summary>
/// 冒烟测试入口：在 Avalonia 主线程启动后，自动注入默认 PG 连接、连接对象树、依次渲染
/// 主窗口/连接管理/连接向导/对象定义/数据编辑等关键界面并截图为 PNG，落盘到 docs/smoke/。
/// 仅用于人工或脚本驱动回归；不影响正常启动路径。
/// </summary>
public static class SmokeHarness
{
    private static string _logFile = string.Empty;
    public static string LogFile => _logFile;

    public static int Run(string[] args)
    {
        // WinExe 子系统没有 console，stdout/stderr 默认不输出；改为写日志文件。
        _logFile = Path.Combine(
            Path.GetDirectoryName(typeof(SmokeHarness).Assembly.Location) ?? AppContext.BaseDirectory,
            "smoke.log");
        try { File.WriteAllText(_logFile, $"[smoke] 启动 {DateTime.Now:HH:mm:ss}\n"); } catch { }

        // 实际工作由 App.OnFrameworkInitializationCompleted 在主窗口就绪后异步驱动。
        return 0;
    }

    private static void AppendLog(string file, string line)
    {
        try { File.AppendAllText(file, line + "\n"); } catch { }
    }

    public static async Task RunAsync(string[] args)
    {
        var logFile = _logFile;
        var ct = CancellationToken.None;
        AppendLog(logFile, "[smoke] RunAsync enter");
        // 等到主窗口就绪（Avalonia 在 UI 线程创建 MainWindow）。
        await Dispatcher.UIThread.InvokeAsync(() => { });
        await Task.Delay(200, ct);
        AppendLog(logFile, "[smoke] UI ready");

        var app = (App)Application.Current!;
        var services = app.Services ?? throw new InvalidOperationException("DI 容器未初始化。");
        var lifetime = (IClassicDesktopStyleApplicationLifetime)app.ApplicationLifetime!;
        var main = lifetime.MainWindow as MainWindow
                   ?? throw new InvalidOperationException("主窗口未创建。");

        var outputDir = ResolveOutputDir(args: Environment.GetCommandLineArgs());
        Directory.CreateDirectory(outputDir);
        AppendLog(logFile, $"[smoke] 输出目录：{outputDir}");

        // 1) 注入默认 PG 连接（环境变量可覆盖）。
        var conn = EnsureDefaultConnection(services);

        // 2) 让主窗口在对象树里能看到这条连接。
        //    注意：MainWindowViewModel 注册为 Transient，必须用窗口的 DataContext，
        //    否则操作的是一个与界面无关的 VM 实例（树不会真正连接/展开）。
        var mainVm = main.DataContext as MainWindowViewModel
                     ?? services.GetRequiredService<MainWindowViewModel>();
        mainVm.RefreshConnections();
        if (conn is not null)
        {
            mainVm.SelectedConnection = conn;
        }

        // 清掉上次冒烟的会话恢复（避免遗留占位 SQL 影响本次截图），
        // 并新建一个干净的空 tab，保证 SelectedQueryTab 不为 null。
        try
        {
            var settings = services.GetService<DatabaseManager.AppCore.Services.IAppSettingsService>()?.Settings;
            if (settings is not null)
            {
                settings.Workspace.Tabs.Clear();
            }
            mainVm.QueryTabs.Clear();
        }
        catch { /* ignore */ }

        // 清空标签页后补一个空查询标签，避免截图时编辑区空白。
        try { mainVm.NewQuery(); } catch { /* ignore */ }

        await SettleAsync(400, ct);

        // 诊断：测量对象树各层级节点的左侧偏移。
        await MeasureTreeDiagAsync(main, logFile);

        // 3) 截主窗口（含对象树、工具栏、状态栏）。
        await CaptureAsync(main, Path.Combine(outputDir, "01_main.png"), ct);

        // 4) 连接管理窗口（VM 已在 DI 注册，直接拿）。
        var connMgrVm = services.GetRequiredService<ConnectionManagerViewModel>();
        await ShowAndCaptureAsync(
            () => new ConnectionManagerWindow(connMgrVm),
            Path.Combine(outputDir, "02_connection_manager.png"),
            ct);

        // 5) 连接向导（新建连接窗口）。
        var connectWin = new ConnectWindow(connMgrVm, null);
        await ShowAndCaptureAsync(() => connectWin, Path.Combine(outputDir, "03_connect_wizard.png"), ct);
        connectWin.Close();

        // 6) 工具窗口：反射创建，让每个窗口走自己的依赖注入（构造器拿自己需要的 VM）。
        var toolShots = new (string FileName, Func<Window> Factory)[]
        {
            ("04_convert.png", () => MakeToolWindow<ConvertWindow>()),
            ("05_schema_compare.png", () => MakeToolWindow<SchemaCompareWindow>()),
            ("06_data_compare.png", () => MakeToolWindow<DataCompareWindow>()),
            ("07_backup.png", () => MakeToolWindow<BackupWindow>()),
            ("08_statistic.png", () => MakeToolWindow<StatisticWindow>()),
            ("09_dependency.png", () => MakeToolWindow<DependencyWindow>()),
            ("10_diagnose.png", () => MakeToolWindow<DiagnoseWindow>()),
            ("11_optimize.png", () => MakeToolWindow<OptimizeWindow>()),
            ("12_index_fragmentation.png", () => MakeToolWindow<IndexFragmentationWindow>()),
            ("13_code_generate.png", () => MakeToolWindow<CodeGenerateWindow>()),
            ("14_column_documentation.png", () => MakeToolWindow<ColumnDocumentationWindow>()),
            ("15_export.png", () => MakeToolWindow<ExportWindow>()),
            ("16_import.png", () => MakeToolWindow<ImportWindow>()),
            ("17_execution_plan.png", () => MakeToolWindow<ExecutionPlanWindow>()),
            ("18_query_history.png", () => MakeToolWindow<QueryHistoryWindow>()),
            ("19_search.png", () => MakeToolWindow<SearchWindow>()),
            ("20_full_data_search.png", () => MakeToolWindow<FullDataSearchWindow>()),
            ("21_table_designer.png", () => MakeTableDesignerWindow()),
            ("22_script_library.png", () => MakeToolWindow<ScriptLibraryWindow>()),
            ("23_dashboard.png", () => MakeToolWindow<DashboardWindow>()),
            ("24_chart.png", () => MakeToolWindow<ChartWindow>()),
        };

        foreach (var (fileName, factory) in toolShots)
        {
            try
            {
                var w = factory();
                if (w is null) continue;
                w.Show();
                await SettleAsync(220, ct);
                await CaptureAsync(w, Path.Combine(outputDir, fileName), ct);
                w.Close();
            }
            catch (Exception ex)
            {
                AppendLog(logFile, $"[smoke] {fileName} 失败：{ex.Message}");
            }
        }

        // 7) 主动连接主窗口中的连接，让对象树展开（截图前）。
        if (conn is not null)
        {
            try
            {
                var node = mainVm.ObjectsExplorer.FindConnectionNode(conn.Name);
                AppendLog(logFile, $"[smoke] 连接节点查找：conn={conn.Name}，node={(node is null ? "未找到" : "已找到")}");
                if (node is not null)
                {
                    await mainVm.ConnectConnectionNodeAsync(node);
                    AppendLog(logFile, $"[smoke] 连接结果：IsConnectionActive={node.IsConnectionActive}，Children={node.Children.Count}");
                }
            }
            catch (Exception ex)
            {
                AppendLog(logFile, $"[smoke] 连接默认库失败：{ex.Message}");
            }
        }
        else
        {
            AppendLog(logFile, "[smoke] 默认连接为 null，跳过连接步骤");
        }
        await SettleAsync(700, ct);
        await CaptureAsync(main, Path.Combine(outputDir, "25_main_connected.png"), ct);

        // 8) 让对象树再展开到 Schema/Tables 层。
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var root in mainVm.ObjectsExplorer.RootNodes.ToList())
                {
                    root.IsExpanded = true;
                    foreach (var db in root.Children.ToList())
                    {
                        db.IsExpanded = true;
                    }
                }
            });
        }
        catch { /* ignore */ }
        await SettleAsync(400, ct);

        // 诊断2：展开后再测各层级偏移。
        await MeasureTreeDiagAsync(main, logFile);

        await CaptureAsync(main, Path.Combine(outputDir, "26_main_tree_expanded.png"), ct);

        // 9) 演示 DB 对象动态着色：注入示例 SQL 与一组假定的"已加载"对象名，
        //     让 DbObjectColorizingTransformer 用红色刷这些标识符，便于视觉验收。
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 演示时新建一个干净的查询标签，避免恢复自上次冒烟的占位 SQL。
                mainVm.NewQuery();

                foreach (var editor in SqlEditor.LiveInstances)
                {
                    editor.SeedDemoSqlText(DemoSql);
                    editor.SeedDemoObjectNames(DemoObjectNames);
                }
            });
        }
        catch (Exception ex)
        {
            AppendLog(logFile, $"[smoke] 注入演示 SQL/对象名失败：{ex.Message}");
        }
        await SettleAsync(400, ct);
        await CaptureAsync(main, Path.Combine(outputDir, "27_query_dbobject_highlight.png"), ct);

        // 10) 专项回归：窗口 Icon 与「已连接状态下」的元数据搜索下拉框。
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var icon = main.Icon;
                AppendLog(logFile, $"[diag] 主窗口 Icon={(icon is null ? "为 null（任务栏将显示默认图标）" : "已加载非空 WindowIcon")}");
            });
        }
        catch (Exception ex)
        {
            AppendLog(logFile, $"[diag] 读取 Icon 失败：{ex.Message}");
        }

        try
        {
            var searchVm = services.GetRequiredService<SearchViewModel>();
            // 与 MainWindow.GetSearchableConnectionNames 一致：全部已保存连接、活动连接排前、去重。
            var searchableNames = mainVm.ObjectsExplorer.RootNodes
                .Where(n => n.NodeType == DbObjectTreeNodeType.Connection && !string.IsNullOrEmpty(n.Name))
                .OrderByDescending(n => n.IsConnectionActive)
                .Select(n => n.Name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            searchVm.SetConnections(searchableNames, searchableNames.FirstOrDefault() ?? string.Empty);
            AppendLog(logFile, $"[diag] 元数据搜索可选连接数={searchableNames.Count}，默认={searchVm.SelectedConnectionName ?? "（空）"}");
            await ShowAndCaptureAsync(() => new SearchWindow(searchVm),
                Path.Combine(outputDir, "28_search_connected.png"), ct);
        }
        catch (Exception ex)
        {
            AppendLog(logFile, $"[smoke] 元数据搜索回归失败：{ex.Message}");
        }

        // 11) 写一份结果摘要（Markdown 表格）。
        WriteResultsIndex(outputDir);
    }

    /// <summary>诊断：输出对象树各层级 TreeViewItem 的模板部件偏移。</summary>
    private static async Task MeasureTreeDiagAsync(MainWindow main, string logFile)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var tree = main.FindControl<TreeView>("ObjectsTree");
                var items = tree?.GetVisualDescendants().OfType<TreeViewItem>().Take(12).ToList() ?? new List<TreeViewItem>();
                AppendLog(logFile, $"[diag] TreeViewItem 数量（最多前12个）：{items.Count}");
                foreach (var item in items)
                {
                    var chevron = item.GetVisualDescendants().FirstOrDefault(d => d.Name == "PART_ExpandCollapseChevronContainer") as Layoutable;
                    var header = item.GetVisualDescendants().FirstOrDefault(d => d.Name == "PART_HeaderPresenter") as Layoutable;
                    AppendLog(logFile,
                        $"[diag] item x={item.Bounds.X:F1} | chevron margin={chevron?.Margin} | header x={header?.Bounds.X:F1} margin={header?.Margin}");
                }
            }
            catch (Exception ex)
            {
                AppendLog(logFile, $"[diag] 失败：{ex.Message}");
            }
        });
    }

    private const string DemoSql =
        "-- DB 对象动态着色演示：与关键字 SELECT/FROM/WHERE 对比\n" +
        "-- 下方的 users / orders / members / products / order_items / v_member_stats\n" +
        "-- 应当用红色（数据库对象）而非蓝色（关键字）显示。\n" +
        "SELECT u.id, u.username, o.total_amount, m.email\n" +
        "FROM users AS u\n" +
        "INNER JOIN orders AS o ON o.user_id = u.id\n" +
        "INNER JOIN members AS m ON m.user_id = u.id\n" +
        "WHERE u.status = 'active'\n" +
        "  AND o.created_at >= '2025-01-01'\n" +
        "ORDER BY o.total_amount DESC\n" +
        "LIMIT 100;\n";

    private static readonly string[] DemoObjectNames = new[]
    {
        "users", "orders", "members", "products", "order_items",
        "v_member_stats", "audit_log", "categories", "tags", "user_roles",
    };

    private static ConnectionItem? EnsureDefaultConnection(IServiceProvider services)
    {
        var connStr = Environment.GetEnvironmentVariable("DBSMOKE_CONNSTR")
            ?? "Host=localhost;Port=5432;USER ID=postgres;Password=1qazXSW@;Database=Member;MaxPoolSize=128;";

        var conn = ParseNpgsql(connStr, name: "PG-Member-Local");

        var connService = services.GetRequiredService<IDbConnectionService>();
        try
        {
            connService.SaveAsync(conn).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AppendLog(_logFile, $"[smoke] 保存默认连接失败：{ex.Message}");
        }
        return connService.GetConnections("Postgres").FirstOrDefault(c => c.Name == conn.Name);
    }

    private static ConnectionItem ParseNpgsql(string connectionString, string name)
    {
        var dict = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

        string Get(params string[] keys)
        {
            foreach (var k in keys)
            {
                if (dict.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v))
                    return v;
            }
            return string.Empty;
        }

        return new ConnectionItem
        {
            DatabaseType = "Postgres",
            Name = name,
            Server = Get("host", "server", "data source"),
            Port = Get("port"),
            Database = Get("database", "initial catalog"),
            UserId = Get("user id", "uid", "user", "username"),
            Password = Get("password", "pwd"),
            IntegratedSecurity = false,
            IsDba = true,
            UseSsl = Get("sslmode").Equals("Require", StringComparison.OrdinalIgnoreCase),
            RememberPassword = true,
        };
    }

    /// <summary>用 DI 解析 VM，再反射注入到工具窗口。</summary>
    private static Window MakeToolWindow<TWindow>() where TWindow : Window
    {
        var services = ((App)Application.Current!).Services!;
        var vmType = FindViewModelTypeForWindow(typeof(TWindow));
        if (vmType is null)
        {
            // 没有显式 VM 关联：退化为无参构造（部分窗口容许）。
            return (TWindow)Activator.CreateInstance(typeof(TWindow))!;
        }
        var vm = services.GetRequiredService(vmType);
        return (TWindow)Activator.CreateInstance(typeof(TWindow), vm)!;
    }

    private static Window MakeTableDesignerWindow()
    {
        // 表设计器需要额外的 table 参数。无 table 时用空表。
        var services = ((App)Application.Current!).Services!;
        var vmType = FindViewModelTypeForWindow(typeof(TableDesignerWindow));
        if (vmType is null)
        {
            return new TableDesignerWindow();
        }
        var vm = services.GetRequiredService(vmType);
        // 多数 TableDesignerWindow 实现接受 (vm, tableInfo?) 之类的参数，这里走无参 fallback。
        try
        {
            return (Window)Activator.CreateInstance(typeof(TableDesignerWindow), vm, null)!;
        }
        catch
        {
            return new TableDesignerWindow();
        }
    }

    private static Window MakeExecutionPlanWindow() => new ExecutionPlanWindow();

    private static Type? FindViewModelTypeForWindow(Type windowType)
    {
        // 寻找一个参数为 ViewModelBase 子类的构造器（VM 注入），取该参数类型作为 DI 解析键。
        foreach (var ctor in windowType.GetConstructors())
        {
            foreach (var p in ctor.GetParameters())
            {
                if (typeof(ViewModelBase).IsAssignableFrom(p.ParameterType))
                {
                    return p.ParameterType;
                }
            }
        }
        return null;
    }

    private static async Task ShowAndCaptureAsync(Func<Window> factory, string file, CancellationToken ct)
    {
        try
        {
            var w = factory();
            w.Show();
            await SettleAsync(280, ct);
            await CaptureAsync(w, file, ct);
            w.Close();
        }
        catch (Exception ex)
            {
                AppendLog(_logFile, $"[smoke] {Path.GetFileName(file)} 失败：{ex.Message}");
            }
    }

    private static async Task CaptureAsync(Window w, string file, CancellationToken ct)
    {
        await SettleAsync(120, ct);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var width = Math.Max(1, (int)w.Bounds.Width);
                var height = Math.Max(1, (int)w.Bounds.Height);
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var pixelSize = new global::Avalonia.PixelSize(width, height);
                using var rt = new RenderTargetBitmap(pixelSize, new global::Avalonia.Vector(96, 96));
                rt.Render(w);
                using var stream = File.Create(file);
                rt.Save(stream);
                AppendLog(_logFile, $"[smoke] {Path.GetFileName(file)} ({width}x{height})");
            }
            catch (Exception ex)
            {
                AppendLog(_logFile, $"[smoke] 截图 {file} 失败：{ex.Message}");
            }
        });
    }

    private static async Task SettleAsync(int delayMs, CancellationToken ct)
    {
        try { await Task.Delay(delayMs, ct); } catch (TaskCanceledException) { }
    }

    private static string ResolveOutputDir(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--smoke-out", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        // bin/Debug/net8.0 → 上溯 4 级到 DatabaseManager.Avalonia，再附加 docs/smoke。
        return Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "smoke");
    }

    private static void WriteResultsIndex(string outputDir)
    {
        var lines = new List<string>
        {
            "# 冒烟测试截图索引",
            "",
            "由 `dotnet run -- --smoke` 自动产出。运行前请确认本地 PostgreSQL `Member` 库可访问。",
            "",
            "| 截图 | 界面 |",
            "| --- | --- |",
        };
        var map = new (string File, string Label)[]
        {
            ("01_main.png", "主窗口（连接前）"),
            ("02_connection_manager.png", "连接管理"),
            ("03_connect_wizard.png", "连接向导"),
            ("04_convert.png", "数据库转换"),
            ("05_schema_compare.png", "结构对比"),
            ("06_data_compare.png", "数据对比"),
            ("07_backup.png", "数据库备份"),
            ("08_statistic.png", "统计"),
            ("09_dependency.png", "依赖分析"),
            ("10_diagnose.png", "数据库诊断"),
            ("11_optimize.png", "数据库优化"),
            ("12_index_fragmentation.png", "索引碎片"),
            ("13_code_generate.png", "代码生成"),
            ("14_column_documentation.png", "列结构文档"),
            ("15_export.png", "数据导出"),
            ("16_import.png", "数据导入"),
            ("17_execution_plan.png", "执行计划"),
            ("18_query_history.png", "查询历史"),
            ("19_search.png", "元数据搜索"),
            ("20_full_data_search.png", "全库数据搜索"),
            ("21_table_designer.png", "表设计器"),
            ("22_script_library.png", "脚本库"),
            ("23_dashboard.png", "仪表盘"),
            ("24_chart.png", "图表窗口"),
            ("25_main_connected.png", "主窗口（已连接）"),
            ("26_main_tree_expanded.png", "主窗口（对象树展开）"),
        };
        foreach (var (file, label) in map)
        {
            lines.Add($"| ![{label}]({file}) | {label} |");
        }
        File.WriteAllText(Path.Combine(outputDir, "README.md"), string.Join("\n", lines));
    }
}
