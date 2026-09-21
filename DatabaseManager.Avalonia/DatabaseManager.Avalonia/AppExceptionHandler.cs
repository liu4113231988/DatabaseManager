using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace DatabaseManager.Avalonia;

/// <summary>
/// 桌面端最后一道异常边界。业务层仍应就近处理可恢复错误；这里只拦截遗漏的
/// UI/异步异常，记录完整诊断信息并向用户显示简洁提示。
/// </summary>
internal static class AppExceptionHandler
{
    private const int MaxUserMessageLength = 600;
    private static int _registered;
    private static int _dialogVisible;

    internal static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DatabaseManager",
        "Logs",
        "application-errors.log");

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            if (IsRecoverable(args.Exception))
            {
                args.Handled = true;
                Report(args.Exception, "界面操作");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Report(args.Exception, "后台任务");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception
                            ?? new InvalidOperationException("发生未知的未处理异常。");
            Report(exception, "应用程序", showDialog: !args.IsTerminating);
        };
    }

    internal static void Report(Exception exception, string context, bool showDialog = true)
    {
        exception = Unwrap(exception);
        if (exception is OperationCanceledException)
            return;

        WriteLog(exception, context);
        if (showDialog)
            QueueDialog(exception, context);
    }

    internal static async void Run(Func<Task> action, string context)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消不是错误。
        }
        catch (Exception ex)
        {
            Report(ex, context);
        }
    }

    internal static string BuildUserMessage(Exception exception, string context)
    {
        exception = Unwrap(exception);
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "发生了未预期的错误。"
            : exception.Message.Trim();
        if (message.Length > MaxUserMessageLength)
            message = message[..MaxUserMessageLength] + "…";

        return $"{context}未能完成，程序已拦截该异常并保持运行。\n\n{message}\n\n详细信息已写入：\n{LogFilePath}";
    }

    private static void QueueDialog(Exception exception, string context)
    {
        if (Interlocked.Exchange(ref _dialogVisible, 1) != 0)
            return;

        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime
                        || lifetime.MainWindow is null)
                    {
                        return;
                    }

                    var box = MessageBoxManager.GetMessageBoxStandard(
                        "操作失败",
                        BuildUserMessage(exception, context),
                        ButtonEnum.Ok,
                        Icon.Error);
                    await box.ShowWindowDialogAsync(lifetime.MainWindow);
                }
                catch (Exception dialogException)
                {
                    WriteLog(dialogException, "显示异常提示");
                }
                finally
                {
                    Interlocked.Exchange(ref _dialogVisible, 0);
                }
            });
        }
        catch (Exception dialogException)
        {
            Interlocked.Exchange(ref _dialogVisible, 0);
            WriteLog(dialogException, "调度异常提示");
        }
    }

    private static void WriteLog(Exception exception, string context)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            var entry = new StringBuilder()
                .AppendLine(new string('-', 80))
                .AppendLine($"Time: {DateTimeOffset.Now:O}")
                .AppendLine($"Context: {context}")
                .AppendLine($"Thread: {Environment.CurrentManagedThreadId}")
                .AppendLine(exception.ToString())
                .ToString();
            File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
        }
        catch
        {
            // 异常处理本身绝不能再次使应用退出。
        }
    }

    private static Exception Unwrap(Exception exception)
        => exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? aggregate
            : exception;

    private static bool IsRecoverable(Exception exception)
        => exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;
}
