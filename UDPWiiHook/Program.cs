using System;
using System.Net.Sockets;

namespace UDPWiiHook
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // Limit ID to range [0x0000, 0xffff] to make IDs consistent between DSU & UDPWii
            ushort id = (ushort)new Random().Next(0x00000, 0x10000);
            Console.WriteLine("[Program] id = {0:X4}", id);

            try
            {
                DSU.Server.CreateInstance(id, 26760);
                UDPWii.Server.servers = new UDPWii.Server[]
                {
                    new UDPWii.Server(id, 0, 4434),
                    new UDPWii.Server(id, 1, 4435),
                    new UDPWii.Server(id, 2, 4436),
                    new UDPWii.Server(id, 3, 4437)
                };
            }
            catch(SocketException)
            {
                Console.Error.WriteLine("[Program] SocketException (running multiple instances?)");
                return;
            }

            DSU.Server.theInstance.Start();
            foreach (UDPWii.Server server in UDPWii.Server.servers)
                server.Start();

            Console.ReadKey();

            foreach (UDPWii.Server server in UDPWii.Server.servers)
                server.Stop();
            DSU.Server.theInstance.Stop();

            Console.ReadKey();
        }
        /*
        // For ICLRRuntimeHost::ExecuteInDefaultAppDomain()
        public static int HostedMain(string args)
        {
            try
            {
                // XXX: A lazy way to process arguments
                Main(new string[] { args });
                return 0;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("[Program] Exception occured:");
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
        }
        */
    }
}
