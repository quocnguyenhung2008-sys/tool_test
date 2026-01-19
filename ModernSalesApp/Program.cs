using OfficeOpenXml;
using System.Windows.Forms;

namespace ModernSalesApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ModernSalesApp");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            try
            {
                AppServices.Logger.Error("ThreadException", args.Exception);
            }
            catch
            {
            }

            MessageBox.Show(
                $"Phần mềm gặp lỗi và sẽ đóng.\n\n{args.Exception.Message}\n\nLog: {Core.AppPaths.LogsDirectory}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            Application.Exit();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                AppServices.Logger.Error("UnhandledException", args.ExceptionObject as Exception);
            }
            catch
            {
            }
        };

        try
        {
            AppServices.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không khởi tạo được dữ liệu.\n\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new WinForms.MainForm());
        }
        catch (Exception ex)
        {
            try
            {
                AppServices.Logger.Error("Startup failed", ex);
            }
            catch
            {
            }

            MessageBox.Show(
                $"Phần mềm không khởi chạy được.\n\n{ex.Message}\n\nLog: {Core.AppPaths.LogsDirectory}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
