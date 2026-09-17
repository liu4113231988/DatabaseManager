using Avalonia.Controls;
using DatabaseManager.AppCore.Services;
using DatabaseManager.AppCore.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>扩展导入：从 ODBC / Access / DBF 抓取稳定 CSV 快照，再进入列映射与导入向导。</summary>
public sealed partial class ExternalImportWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private string? _snapshot;

    public ExternalImportWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        _shell.Bind(ChooseSourceButton, async _ =>
        {
            var path = await _shell.OpenPath("选择 Access / DBF 文件");
            if (path is not null) SourceBox.Text = path;
        });
        _shell.Bind(SnapshotButton, async ct =>
        {
            var path = await _shell.SavePath("import-snapshot.csv"); if (path is null) return;
            if (File.Exists(SourceBox.Text) && string.Equals(Path.GetFullPath(SourceBox.Text!), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("快照不能覆盖源文件。");
            _snapshot = null;
            int rows = TypeBox.SelectedIndex == 2
                ? await ExternalImportSource.SnapshotDbfAsync(SourceBox.Text!, path, EncodingBox.Text!, ct)
                : await ExternalImportSource.SnapshotOdbcAsync(TypeBox.SelectedIndex == 1
                    ? ExternalImportSource.AccessConnectionString(SourceBox.Text!, DriverBox.Text!)
                    : SourceBox.Text!, SelectSqlBox.Text!, path, ct);
            _snapshot = path;
            _shell.SetOutput($"快照生成完成：{rows} 行\n{path}\n续导时使用同一快照，不要重新抓取变化中的源数据。");
        });
        _shell.Bind(OpenSnapshotButton, async _ => { _snapshot = await _shell.OpenPath("选择已有 CSV 快照"); });
        _shell.Bind(MapImportButton, async _ =>
        {
            if (_snapshot is null || !File.Exists(_snapshot)) throw new InvalidOperationException("请先生成或选择快照。");
            var viewModel = _shell.Services.GetRequiredService<ImportViewModel>();
            viewModel.SetFilePath(_snapshot);
            await new ImportWindow(viewModel).ShowDialog<object?>(this);
        });
        _shell.Initialize();
    }
}
