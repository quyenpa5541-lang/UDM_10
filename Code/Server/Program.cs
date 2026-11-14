using System;
using System.Windows.Forms;
using Shared;

namespace Server;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            _ = AppSettings.Load();
            Application.Run(new ServerForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể khởi chạy Server do cấu hình không hợp lệ:\n" + ex.Message,
                "UDM_10 - Lỗi cấu hình",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
