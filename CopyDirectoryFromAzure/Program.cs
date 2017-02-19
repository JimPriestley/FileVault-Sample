using System;
using System.Configuration;
using System.Linq;

namespace CopyDirectoryFromAzure
{
    class Program
    {
        static void Main(string[] args)
        {

            if (args.Count() == 0)
            {
                Console.WriteLine("Usage: CopyDirectoryFromAzure <DestinationPath> <ContainerName> <SearchPrefix * for all blobs in container>");
                Console.WriteLine("Press Any Key to Continue.");
                Console.ReadKey();
                Environment.Exit(-1);
            }

            FileVault.FileVault Fv = new FileVault.FileVault(ConfigurationManager.AppSettings["accountName"], ConfigurationManager.AppSettings["accountKey"]);

            Fv.GetBlobs(args[0], args[1], args[2]);
        }
    }
}
