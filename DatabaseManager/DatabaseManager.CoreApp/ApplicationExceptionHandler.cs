using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DatabaseManager
{
    /// <summary>WinForms 兼容客户端的最后一道异常边界。</summary>
    internal static class ApplicationExceptionHandler
    {
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
            {
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) => Report(args.Exception, "界面操作");
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                Report(args.Exception, "后台任务");
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var exception = args.ExceptionObject as Exception
                                ?? new InvalidOperationException("发生未知的未处理异常。");
                WriteLog(exception, "应用程序");
            };
        }

        internal static void Report(Exception exception, string context, bool showDialog = true)
        {
            exception = Unwrap(exception);
            if (exception is OperationCanceledException)
            {
                return;
            }

            WriteLog(exception, context);
            if (showDialog)
            {
                ShowDialog(exception, context);
            }
        }

        private static void ShowDialog(Exception exception, string context)
        {
            if (Interlocked.Exchange(ref _dialogVisible, 1) != 0)
            {
                return;
            }

            void Show()
            {
                try
                {
                    var message = exception.Message;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = "发生了未预期的错误。";
                    }
                    else if (message.Length > 600)
                    {
                        message = message.Substring(0, 600) + "…";
                    }

                    MessageBox.Show(
                        $"{context}未能完成，程序已拦截该异常并保持运行。\r\n\r\n{message}\r\n\r\n详细信息已写入：\r\n{LogFilePath}",
                        "操作失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch (Exception dialogException)
                {
                    WriteLog(dialogException, "显示异常提示");
                }
                finally
                {
                    Interlocked.Exchange(ref _dialogVisible, 0);
                }
            }

            try
            {
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
                {
                    Application.OpenForms[0].BeginInvoke((Action)Show);
                }
                else
                {
                    Show();
                }
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
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));
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
                // 异常处理本身不能再次导致应用退出。
            }
        }

        private static Exception Unwrap(Exception exception)
        {
            var aggregate = exception as AggregateException;
            if (aggregate == null)
            {
                return exception;
            }

            aggregate = aggregate.Flatten();
            return aggregate.InnerExceptions.Count > 0 ? aggregate.InnerExceptions[0] : aggregate;
        }
    }
}
