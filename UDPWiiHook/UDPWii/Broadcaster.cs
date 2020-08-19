using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;

namespace UDPWiiHook.UDPWii
{
    internal class Broadcaster
    {
        private readonly Timer timer;
        private readonly UdpClient udp;
        private readonly byte[] packetData;
        private readonly IPEndPoint endPoint;

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            udp.Send(packetData, packetData.Length, endPoint);
        }

        public Broadcaster(string name, ushort id, byte slot, ushort port, ushort portBroadcast = 4431, uint interval = 1500)
        {
            UDPWii.Broadcast packet = new UDPWii.Broadcast();
            packet.slot = slot;
            packet.port = Util.ConvEndianUInt16(port);

            // Let Wiimote ID derive from DSU ID & slot number
            packet.id = Util.ConvEndianUInt16((ushort)(id + slot));

            byte[] nameData = Encoding.UTF8.GetBytes(name);
            this.packetData = packet.Finish(nameData);

            this.endPoint = new IPEndPoint(IPAddress.Broadcast, portBroadcast);
            this.udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            this.udp.EnableBroadcast = true;

            this.timer = new Timer(interval);
            this.timer.Elapsed += timer_Elapsed;
        }

        ~Broadcaster()
        {
            if (timer.Enabled)
                timer.Stop();
            udp.Close();
        }

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }
    }
}
