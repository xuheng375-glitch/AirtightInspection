using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AirtightInspection.Config;
using AirtightInspection.Data;
using AirtightInspection.Forms;
using NLog;

namespace AirtightInspection
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var singleInstance = new Mutex(true, @"Local\AirtightInspection.DataAcquisition", out var isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show("气密检测数据采集系统已经在运行，请勿重复启动。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(Path.Combine(baseDirectory, "Logs"));
                var config = new AppConfig(baseDirectory); Directory.CreateDirectory(config.ManualFolder);
                var database = new Database(config.DatabasePath); database.Initialize();
                Application.Run(new MainForm(config, database));
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Fatal(ex, "程序启动失败");
                MessageBox.Show("程序启动失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { LogManager.Shutdown(); }
        }
    }
}
