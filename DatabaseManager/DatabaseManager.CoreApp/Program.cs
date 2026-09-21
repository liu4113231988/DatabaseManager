using DatabaseInterpreter.Core;
using DatabaseManager.Core;
using DatabaseManager.Helper;
using DatabaseManager.Forms;
using DatabaseManager.Profile.Manager;
using System;
using System.Windows.Forms;

namespace DatabaseManager
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationExceptionHandler.Register();

            try
            {
                DbInterpreter.Setting = SettingManager.GetInterpreterSetting();

                ProfileBaseManager.Init();

                AntdUiThemeHelper.ApplyGlobalTheme(SettingManager.Setting.ThemeOption.ThemeType);

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new frmMain());
            }
            catch (Exception ex)
            {
                ApplicationExceptionHandler.Report(ex, "应用启动");
            }
        }
    }
}
