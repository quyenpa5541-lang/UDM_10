using System;
using System.IO;
using System.Threading.Tasks;

using Shared;

namespace Server
{
    class Program
    {
        public static void Main(string[] args)
        {
            AppSettings settings = new AppSettings(); 

            ManageClients manageClients = new ManageClients(settings);

            _ = Task.Run(() => manageClients.AcceptClients());
        }
    }
}