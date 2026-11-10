using System;
using System.Threading.Tasks;
using System.Windows.Forms;

using Shared;

namespace Server
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppSettings settings = new AppSettings();

            ManageClients manageClients = new ManageClients(settings);

            _ = Task.Run(() => manageClients.AcceptClients());

            Application.Run(new ServerForm());
        }
    }
}
