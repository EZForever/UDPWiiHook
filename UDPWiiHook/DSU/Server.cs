using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace UDPWiiHook.DSU
{
    // This struct is a class since it needs to be reference-able
    internal class Client
    {
        public DateTime lastSeenAll;
        public DateTime[] lastSeenSlot;

        public Client()
        {
            lastSeenSlot = new DateTime[4];

            lastSeenAll = DateTime.MinValue;
            for (int i = 0; i < 4; i++)
                lastSeenSlot[i] = DateTime.MinValue;
        }
    }

    internal class Server
    {
        public static Server theInstance;

        public static void CreateInstance(uint id, ushort port)
        {
            theInstance = new Server(id, port);
        }

        // ---

        private uint id;
        private readonly UdpClient udpSend; // For sending data packets
        private readonly UdpClient udpRecv; // For receiving & sending control packets
        public Dictionary<IPEndPoint, Client> clients;
        private CancellationTokenSource tokenSource;
        private Task task;

        private Server(uint id, ushort port = 26760)
        {
            this.id = id;
            this.clients = new Dictionary<IPEndPoint, Client>();

            this.udpSend = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            this.udpRecv = new UdpClient(new IPEndPoint(IPAddress.Any, port));

            Console.WriteLine("[DSU.Server] Initialized at port {0}", port);
        }

        ~Server()
        {
            Stop();
            udpSend.Close();
            udpRecv.Close();
        }

        private void OnRecv(IPEndPoint endPointRemote, byte[] data)
        {
            try
            {
                DSU.Header header = Util.Deserialize<DSU.Header>(data);
                if (!header.IsValid(data) || header.magic != DSU.Header.Magic.DSUC)
                    throw new ArgumentException("!header.IsValid()");

                //Console.WriteLine("[DSU.Server] Recvd packet from {0}: {1}", endPointRemote.ToString(), header.type);
                switch (header.type)
                {
                    case Header.PacketType.Version:
                        {
                            //var packet = Util.Deserialize<DSU.VersionReq>(data); // Useless for an empty packet
                            byte[] replyData = new DSU.VersionRsp(id).Finish();
                            udpRecv.Send(replyData, replyData.Length, endPointRemote);
                            break;
                        }
                    case Header.PacketType.Slot:
                        {
                            DSU.SlotReq packet = Util.Deserialize<DSU.SlotReq>(data);
                            byte[] slots = packet.GetLeftoverData(data);

                            DSU.SlotRsp reply = new SlotRsp(id);
                            byte[] replyData;
                            for(int i = 0; i < Math.Min(packet.count, slots.Length); i++)
                            {
                                reply.slot = UDPWii.DataDSU.SlotDSU(UDPWii.Server.servers[slots[i]].client);
                                replyData = reply.Finish();
                                udpRecv.Send(replyData, replyData.Length, endPointRemote);
                            }
                            break;
                        }
                    case Header.PacketType.Data:
                        {
                            DSU.DataReq packet = Util.Deserialize<DSU.DataReq>(data);

                            lock (clients)
                            {
                                // NOTE: IPEndPoint & IPAddress don't override operator==
                                //       Containers.Generic uses IEqualityComparer (.Equals()) so it's fine
                                if(!clients.TryGetValue(endPointRemote, out Client client))
                                {
                                    Console.WriteLine("[DSU.Server] Creating new client " + endPointRemote.ToString());
                                    client = new DSU.Client();
                                    clients.Add(endPointRemote, client);
                                }
                                
                                switch(packet.action)
                                {
                                    case DataReq.Action.SubscribeAll:
                                        {
                                            client.lastSeenAll = DateTime.Now;
                                            break;
                                        }
                                    case DataReq.Action.SubscribeSlot:
                                        {
                                            client.lastSeenSlot[packet.slot] = DateTime.Now;
                                            break;
                                        }
                                    case DataReq.Action.SubscribeMAC:
                                    default:
                                        throw new NotImplementedException("MAC subscription not supported");
                                }
                            }
                            break;
                        }
                    default:
                        throw new NotImplementedException("Not possible");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[DSU.Server] Dropping invalid packet from {0}: {1}: {2}", endPointRemote.ToString(), e.GetType().Name, e.Message);
            }
        }

        private void TaskMain(CancellationToken token)
        {
            try
            {
                Task<UdpReceiveResult> taskRecv;
                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    taskRecv = udpRecv.ReceiveAsync();
                    taskRecv.Wait(token);

                    OnRecv(taskRecv.Result.RemoteEndPoint, taskRecv.Result.Buffer);
                }
            }
            catch (OperationCanceledException)
            {
                ;
            }
        }

        // This routine with be run synchronously with UDPWii.Server
        public void SendDataPacket(UDPWii.DataDSU packet)
        {
            // If the server is not running, just drop the packet
            if (tokenSource == null)
                return;

            lock (clients)
            {
                // If there's no client, skip the packet processing step and drop the packet
                if (clients.Count == 0)
                    return;

                List<IPEndPoint> toRemove = new List<IPEndPoint>();

                DSU.DataRsp dsuPacket = packet.ToDSU(id);
                byte[] dsuPacketData = dsuPacket.Finish();
                //Console.WriteLine("[DSU.Sender] ({0}, {1})", dsuPacket.touch1.x, dsuPacket.touch1.y);

                foreach (var keyValuePair in clients)
                {
                    Client client = keyValuePair.Value;
                    if (!Util.IsTimeout(client.lastSeenAll) || !Util.IsTimeout(client.lastSeenSlot[packet.client.slot]))
                    {
                        udpSend.Send(dsuPacketData, dsuPacketData.Length, keyValuePair.Key);
                    }
                    else
                    {
                        // Is this client completely dead? If so, mark it for removal
                        // Reference: https://github.com/Davidobot/BetterJoy/blob/master/BetterJoyForCemu/UpdServer.cs

                        // We're here so lastSeenAll must have timed out
                        if (client.lastSeenSlot.All(Util.IsTimeout))
                            toRemove.Add(keyValuePair.Key);
                    }
                    //Console.WriteLine("[DSU.Sender] Packet sent to " + keyValuePair.Key.ToString());
                }

                // Clear all dead clients now to prevent concurrent modification
                foreach (IPEndPoint key in toRemove)
                {
                    Console.WriteLine("[DSU.Server] Removing dead client {0}", key.ToString());
                    clients.Remove(key);
                }
            }
        }

        public void Start()
        {
            Stop();

            tokenSource = new CancellationTokenSource();
            task = Task.Run(() => TaskMain(tokenSource.Token), tokenSource.Token);
        }

        public void Stop()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                task.Wait();
            }

            task = null;
            tokenSource = null;
        }
    }
}
