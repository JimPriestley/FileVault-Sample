using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace CopyDirectory2Azure
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Count() == 0)
            {
                Console.WriteLine("Usage: CopyDirectory2Azure <DirectoryPath> <ContainerName>");
                Console.WriteLine("Press Any Key to Continue.");
                Console.ReadKey();
                Environment.Exit(-1);
            }

            FileVault.FileVault Fv = new FileVault.FileVault(ConfigurationManager.AppSettings["accountName"], ConfigurationManager.AppSettings["accountKey"]);

            Fv.WriteDirectory(args[0], true, args[1]);

            
        }
    }
}
