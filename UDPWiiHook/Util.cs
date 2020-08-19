using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UDPWiiHook
{
    internal static class Util
    {
        #region Serialization things

        public static T Deserialize<T>(byte[] data)
        {
            int length = Marshal.SizeOf(typeof(T));
            IntPtr pData = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.Copy(data, 0, pData, length);
                return (T)Marshal.PtrToStructure(pData, typeof(T));
            }
            catch (Exception e)
            {
                Console.WriteLine("[Util] Deserialization failed: {0}:{1}", e.GetType().Name, e.Message);
                return default;
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }
        }

#if false
        public static void SerializationTest()
        {
            Dictionary<Type, int> toTest = new Dictionary<Type, int>
            {
                { typeof(DSU.VersionReq), 20 },
                { typeof(DSU.VersionRsp), (20 + 2) },
                { typeof(DSU.SlotRsp), (20 + 12) },
                { typeof(DSU.DataRsp), 100 }
            };

            foreach(Type clazz in toTest.Keys)
            {
                Console.Write(clazz.Name);
                if(Marshal.SizeOf(clazz) != toTest[clazz])
                {
                    Console.WriteLine(" failed: {0} != {1}", Marshal.SizeOf(clazz), toTest[clazz]);
                }
                else
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine("Test finished");
            //Console.ReadKey();

            byte[] data = { 0x31, 0x32, 0x33, 0x34 };
            Console.WriteLine("{0:x8}", DSU.Crc32.Get(data));
            Console.WriteLine("{0:x8}", DSU.Crc32.Get(data));
            //Console.ReadKey();

            DSU.VersionRsp packet = new DSU.VersionRsp(0x1234);
            byte[] packetData = packet.Finish();
            Console.WriteLine(BitConverter.ToString(packetData));
            var packet2 = Util.Deserialize<DSU.VersionRsp>(packetData);
            Console.WriteLine(packet2.maxVersion);
            Console.WriteLine(packet2.IsValid(packetData));
            //Console.ReadKey();
        }
#endif

        #endregion

        #region Endian conversion

        public static UInt16 ConvEndianUInt16(UInt16 value)
        {
            return (UInt16)(((value & 0xff) << 8) | (value >> 8));
        }

        public static UInt16 ReadUInt16BigEndian(this BinaryReader reader)
        {
            byte[] data = reader.ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        public static UInt32 ReadUInt32BigEndian(this BinaryReader reader)
        {
            byte[] data = reader.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public static UInt64 ReadUInt64BigEndian(this BinaryReader reader)
        {
            byte[] data = reader.ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        public static Int32 ReadInt32BigEndian(this BinaryReader reader)
        {
            byte[] data = reader.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        #endregion

        #region Timeout check

        public static bool IsTimeout(DateTime time)
        {
            return (DateTime.Now - time).TotalSeconds >= 5;
        }
        /*
        private static DateTime ts;

        public static void TimingStart()
        {
            ts = DateTime.Now;
        }

        public static double TimingEnd()
        {
            return (DateTime.Now - ts).TotalMilliseconds;
        }
        */
        #endregion
    }
}
