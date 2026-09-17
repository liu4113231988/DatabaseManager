using Avalonia.Controls;
using Avalonia.Input.Platform;
using DatabaseManager.AppCore.Services;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>AI SQL 助手：把请求、SQL 与选中的表元数据发送到 OpenAI 兼容接口，仅预览回答。</summary>
public sealed partial class AiSqlWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private readonly Dictionary<string, string> _selectedMetadata = new();

    public AiSqlWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        _shell.Bind(AddMetadataButton, async ct =>
        {
            var connection = _shell.Connection; var table = _shell.Table;
            var metadata = await _shell.Edits.GetTableMetadataAsync(connection.Name, connection.Database, table.Name, table.Schema, ct);
            if (!metadata.IsSuccess) throw new InvalidOperationException(metadata.ErrorMessage);
            _selectedMetadata[table.DisplayName] = table.DisplayName + " (" + string.Join(", ", metadata.TableInfo.Columns.Select(x => x.Name + " " + x.DataType)) + ")";
            SchemaTextBox.Text = "方言：" + connection.DatabaseType + "\n" + string.Join("\n", _selectedMetadata.Values);
            ConsentBox.IsChecked = false;
        });
        _shell.Bind(ClearMetadataButton, _ => { _selectedMetadata.Clear(); SchemaTextBox.Text = ""; ConsentBox.IsChecked = false; return Task.CompletedTask; });
        _shell.Bind(SaveSettingsButton, _ => { WorkbenchSupport.SaveSettings("ai-sql.json", Settings()); return Task.CompletedTask; });
        _shell.Bind(LoadSettingsButton, _ =>
        {
            var settings = WorkbenchSupport.LoadSettings<AiSqlSettings>("ai-sql.json");
            EndpointBox.Text = settings.Endpoint; ModelBox.Text = settings.Model;
            KeyEnvBox.Text = settings.ApiKeyEnvironmentVariable; ConsentBox.IsChecked = false;
            return Task.CompletedTask;
        });
        _shell.Bind(AskButton, async ct =>
        {
            if (ConsentBox.IsChecked != true) throw new InvalidOperationException("请先确认允许发送所选内容。");
            _shell.SetOutput(await AiSqlAssistant.AskAsync(Settings(), ModeBox.SelectedItem?.ToString() ?? "生成 SQL", RequestBox.Text ?? "", SqlBox.Text ?? "", SchemaTextBox.Text ?? "", ct));
        });
        CopyButton.Click += async (_, _) => { if (Clipboard is not null) await Clipboard.SetTextAsync(OperationOutput.Text ?? ""); };
        _shell.Initialize();
    }

    private AiSqlSettings Settings() => new()
    {
        Endpoint = EndpointBox.Text?.Trim() ?? "",
        Model = ModelBox.Text?.Trim() ?? "",
        ApiKeyEnvironmentVariable = KeyEnvBox.Text?.Trim() ?? ""
    };
}
