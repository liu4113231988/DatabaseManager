using Avalonia.Controls;
using DatabaseManager.AppCore.Models;
using DatabaseManager.AppCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseManager.Avalonia.Views.Workbench;

/// <summary>数据字典：按模板生成对象目录 / 列属性说明，导出文档或创建每日文档计划。</summary>
public sealed partial class DictionaryWindow : Window
{
    private readonly WorkbenchSupport _shell;
    private DictionaryDocument? _document;

    public DictionaryWindow(IServiceProvider services, string? connectionName = null, string? database = null)
    {
        InitializeComponent();
        _shell = new WorkbenchSupport
        {
            Window = this, Services = services, ConnectionBox = ConnectionBox, DatabaseBox = DatabaseBox, LoadButton = LoadButton,
            TableBox = TableBox, OutputBox = OperationOutput, ResultGrid = PreviewGrid, StatusText = OperationStatus,
            CancelButton = CancelButton, BodyGrid = BodyGrid, PageHost = PageScroll,
            InitialConnection = connectionName, InitialDatabase = database
        };

        var defaults = new DictionaryOptions();
        TitleBox.Text = defaults.Title;
        ObjectTemplateBox.Text = defaults.ObjectTemplate;
        ColumnTemplateBox.Text = defaults.ColumnTemplate;

        _shell.Bind(AddObjectButton, _ =>
        {
            var names = Options().Objects;
            if (!names.Contains(_shell.Table.DisplayName)) names.Add(_shell.Table.DisplayName);
            ObjectsBox.Text = string.Join(",", names);
            return Task.CompletedTask;
        });
        _shell.Bind(GenerateButton, async ct =>
        {
            _document = await DataDictionaryService.ReadAsync(_shell.Connection, Options(), ct);
            _shell.SetOutput(string.Join("\n", _document.Lines));
        });
        _shell.Bind(ExportButton, async ct =>
        {
            if (_document is null) throw new InvalidOperationException("请先生成预览。");
            var path = await _shell.SavePath("data-dictionary." + Format());
            if (path is not null) await DataDictionaryService.SaveAsync(_document, path, ct);
        });
        _shell.Bind(SaveTemplateButton, _ => { WorkbenchSupport.SaveSettings("dictionary-template.json", Options()); return Task.CompletedTask; });
        _shell.Bind(LoadTemplateButton, _ =>
        {
            var options = WorkbenchSupport.LoadSettings<DictionaryOptions>("dictionary-template.json");
            TitleBox.Text = options.Title; IntroBox.Text = options.Introduction;
            ObjectTemplateBox.Text = options.ObjectTemplate; ColumnTemplateBox.Text = options.ColumnTemplate;
            ObjectsBox.Text = string.Join(",", options.Objects); _document = null;
            return Task.CompletedTask;
        });
        _shell.Bind(ScheduleButton, async ct =>
        {
            var path = await _shell.SavePath("data-dictionary." + Format()); if (path is null) return;
            var connection = _shell.Connection;
            var schedule = new ScheduleDefinition
            {
                Name = "数据字典 " + connection.Database, ConnectionName = connection.Name, DatabaseName = connection.Database,
                TaskType = ScheduleTaskTypes.Documentation, ExportFilePath = path, Dictionary = Options(), Enabled = false
            };
            _shell.Services.GetRequiredService<IScheduleService>().Save(schedule);
            _shell.SetOutput("已创建停用状态的每日 02:00 文档计划。请在任务定时调度中核对输出路径、时间并启用；运行失败会记录在任务中心。");
        });
        _shell.Initialize();
    }

    private string Format() => (FormatBox.SelectedItem?.ToString() ?? "PDF").ToLowerInvariant();

    private DictionaryOptions Options() => new()
    {
        Title = TitleBox.Text ?? "数据字典",
        Introduction = IntroBox.Text ?? "",
        ObjectTemplate = ObjectTemplateBox.Text ?? "",
        ColumnTemplate = ColumnTemplateBox.Text ?? "",
        Objects = (ObjectsBox.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
    };
}
