using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UDPWiiHook.UDPWii
{
    // NOTE: All the integers are of network-order

    #region Broadcast

    // NOTE: This is ill-defined and requires manual construction of data on serialization
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Broadcast
    {
        private readonly Byte magic = 0xdf;
        public UInt16 id; // "Unique to each Wiimote"
        public Byte slot;
        public UInt16 port;
        private Byte nameLength;

        // NOTE: Leftover data
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 0)] public Byte[] name;

        public byte[] Finish(Byte[] name)
        {
            nameLength = (Byte)name.Length;
            int realLength = Marshal.SizeOf(typeof(Broadcast)) + name.Length;

            byte[] data = new byte[realLength];
            IntPtr pData = Marshal.AllocHGlobal(realLength);

            Marshal.StructureToPtr(this, pData, true);
            Marshal.Copy(pData, data, 0, realLength);
            Array.Copy(name, 0, data, Marshal.SizeOf(typeof(Broadcast)), name.Length);
            Marshal.FreeHGlobal(pData);

            return data;
        }
    }

    #endregion

    #region Data

    // NOTE: This struct is ill-defined and for deserialization only
    public class Data
    {
        #region Inner types

        [Flags]
        public enum Flags : UInt16
        {
            None = 0,

            Accel        = 1 << 0,
            Buttons      = 1 << 1,
            IR           = 1 << 2,
            Nunchuk      = 1 << 3,
            NunchukAccel = 1 << 4,

            // Extensions
            AccelTS = 1 << 5,
            Gyro    = 1 << 6
        }

        [Flags]
        public enum Buttons : UInt32
        {
            None = 0,

            B1 = 1 << 0,  // 1
            B2 = 1 << 1,  // 2
            BA = 1 << 2,  // A
            BB = 1 << 3,  // B
            BP = 1 << 4,  // +
            BM = 1 << 5,  // -
            BH = 1 << 6,  // Home
            BU = 1 << 7,  // Up
            BD = 1 << 8,  // Down
            BL = 1 << 9,  // Left
            BR = 1 << 10, // Right
            SK = 1 << 11  // Power switch / Sync?
        }

        public struct Accel
        {
            public Int32 x;
            public Int32 y;
            public Int32 z;
        }

        public struct IR
        {
            public Int32 x;
            public Int32 y;
        }

        public struct Nunchuk
        {
            #region Inner types

            [Flags]
            public enum Buttons : Byte
            {
                None = 0,

                NC = 1 << 0, // C
                NZ = 1 << 1  // Z
            }

            #endregion

            public Buttons buttons;
            public Int32 x;
            public Int32 y;
        }

        public struct NunchukAccel
        {
            public Int32 x;
            public Int32 y;
            public Int32 z;
        }

        public struct Gyro
        {
            public Int32 pitch;
            public Int32 yaw;
            public Int32 roll;
        }

        #endregion

        private readonly Byte magic = 0xde;
        public Flags flags;
        public Accel accel;
        public Buttons buttons;
        public IR ir;
        public Nunchuk nunchuk;
        public NunchukAccel nunchukAccel;

        // Extensions
        public UInt64 accelTimestamp;
        public Gyro gyro;

        public Data(byte[] packet)
        {
            using(MemoryStream ms = new MemoryStream(packet))
            using(BinaryReader reader = new BinaryReader(ms))
            {
                try
                {
                    Byte magic = reader.ReadByte();
                    if (magic != this.magic)
                        throw new ArgumentException("Magic mismatch");
                    
                    this.flags = (Flags)reader.ReadUInt16BigEndian();
                    
                    if ((flags & Flags.Accel) != 0)
                    {
                        this.accel.x = reader.ReadInt32BigEndian();
                        this.accel.y = reader.ReadInt32BigEndian();
                        this.accel.z = reader.ReadInt32BigEndian();
                    }

                    if ((flags & Flags.Buttons) != 0)
                    {
                        this.buttons = (Buttons)reader.ReadUInt32BigEndian();
                    }

                    if ((flags & Flags.IR) != 0)
                    {
                        this.ir.x = reader.ReadInt32BigEndian();
                        this.ir.y = reader.ReadInt32BigEndian();
                    }

                    if ((flags & Flags.Nunchuk) != 0)
                    {
                        this.nunchuk.buttons = (Nunchuk.Buttons)reader.ReadByte();
                        this.nunchuk.x = reader.ReadInt32BigEndian();
                        this.nunchuk.y = reader.ReadInt32BigEndian();
                    }

                    if ((flags & Flags.NunchukAccel) != 0)
                    {
                        this.nunchukAccel.x = reader.ReadInt32BigEndian();
                        this.nunchukAccel.y = reader.ReadInt32BigEndian();
                        this.nunchukAccel.z = reader.ReadInt32BigEndian();
                    }

                    if ((flags & Flags.AccelTS) != 0)
                    {
                        this.accelTimestamp = reader.ReadUInt64BigEndian();
                    }

                    if ((flags & Flags.Gyro) != 0)
                    {
                        this.gyro.pitch = reader.ReadInt32BigEndian();
                        this.gyro.yaw = reader.ReadInt32BigEndian();
                        this.gyro.roll = reader.ReadInt32BigEndian();
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                    // In case of any exception occurs, render this packet invalid
                    this.flags = Flags.None;
                }
            }
        }
    }

    // This struct is for passing data to DSU.Sender
    // Then data processing happens only on the DSU side (since DSU.Sender is not time-limited)
    internal class DataDSU : Data
    {
        public static DSU.Slot SlotDSU(UDPWii.Client client)
        {
            //Console.WriteLine("[UDPWii.DataDSU] slot = {0}, seenDelta = {1}", slot, (DateTime.Now - client.lastSeen).TotalSeconds);
            DSU.Slot slotData = new DSU.Slot();
            slotData.mac = new Byte[6];

            slotData.slot = client.slot;
            slotData.state = Util.IsTimeout(client.lastSeen) ? DSU.Slot.State.NotConnected : DSU.Slot.State.Connected;
            slotData.model = client.gyroSeen ? DSU.Slot.Model.FullGyro : DSU.Slot.Model.PartialGyro;
            slotData.connection = DSU.Slot.Connection.USB;
            Array.Clear(slotData.mac, 0, 6); // Necessary?
            slotData.battery = DSU.Slot.Battery.High;

            return slotData;
        }

        // ---
        
        public UDPWii.Client client;

        public DataDSU(byte[] packet, UDPWii.Client client)
            : base(packet)
        {
            this.client = client;
        }

        public DSU.DataRsp ToDSU(UInt32 id)
        {
            DSU.DataRsp packet = new DSU.DataRsp(id);

            // Buttons (& Analog)
            // XXX: Packet w/o button info == No button pressed
            if ((flags & Flags.Buttons) != 0)
            {
                /*
                if ((buttons & Buttons.B1) != 0) packet.buttons |= DSU.DataRsp.Buttons.X;
                if ((buttons & Buttons.B2) != 0) packet.buttons |= DSU.DataRsp.Buttons.Y;
                if ((buttons & Buttons.BA) != 0) packet.buttons |= DSU.DataRsp.Buttons.A;
                if ((buttons & Buttons.BB) != 0) packet.buttons |= DSU.DataRsp.Buttons.B;
                if ((buttons & Buttons.BP) != 0) packet.buttons |= DSU.DataRsp.Buttons.L1;
                if ((buttons & Buttons.BM) != 0) packet.buttons |= DSU.DataRsp.Buttons.R1;
                if ((buttons & Buttons.BH) != 0) packet.buttons |= DSU.DataRsp.Buttons.Options;
                if ((buttons & Buttons.BU) != 0) packet.buttons |= DSU.DataRsp.Buttons.DPadUp;
                if ((buttons & Buttons.BD) != 0) packet.buttons |= DSU.DataRsp.Buttons.DPadDown;
                if ((buttons & Buttons.BL) != 0) packet.buttons |= DSU.DataRsp.Buttons.DPadLeft;
                if ((buttons & Buttons.BR) != 0) packet.buttons |= DSU.DataRsp.Buttons.DPadRight;
                if ((buttons & Buttons.SK) != 0) throw new NotImplementedException("UDPWii.DataDSU.ToDSU(): WTH is button SK?");
                */

                /*
                    New button mappings:
                        L, D, R, U => Analog.{L, D, R, U}
                        A, B, 1, 2 => Analog.{A, B, X, Y}
                        P, M, H    => Buttons.{R3, L3, Options}
                        SK         => Buttons.Share
                */
                if ((buttons & Buttons.BL) != 0) packet.analog.DPadLeft = 0xff;
                if ((buttons & Buttons.BD) != 0) packet.analog.DPadDown = 0xff;
                if ((buttons & Buttons.BR) != 0) packet.analog.DPadRight = 0xff;
                if ((buttons & Buttons.BU) != 0) packet.analog.DPadUp = 0xff;
                if ((buttons & Buttons.BA) != 0) packet.analog.A = 0xff;
                if ((buttons & Buttons.BB) != 0) packet.analog.B = 0xff;
                if ((buttons & Buttons.B1) != 0) packet.analog.X = 0xff;
                if ((buttons & Buttons.B2) != 0) packet.analog.Y = 0xff;
                if ((buttons & Buttons.BP) != 0) packet.buttons |= DSU.DataRsp.Buttons.R3;
                if ((buttons & Buttons.BM) != 0) packet.buttons |= DSU.DataRsp.Buttons.L3;
                if ((buttons & Buttons.BH) != 0) packet.buttons |= DSU.DataRsp.Buttons.Options;
                if ((buttons & Buttons.SK) != 0) packet.buttons |= DSU.DataRsp.Buttons.Share;
            }

            // Stick (ignored)
            // Set sticks in the middle
            packet.stick.leftX = packet.stick.leftY = 0x80;
            packet.stick.rightX = packet.stick.rightY = 0x80;

            // Touch1 (using IR data)
            if ((flags & Flags.IR) != 0)
            {
                client.touching = true;

                packet.touch1.active = 1;
                packet.touch1.id = client.touchId;

                // UDPWii: x, y in [0, 1], (0, 0) at bottom left, absloute
                // DSU: x, y in [-1000, 1000]*[-500, 500] (TOUCH_(X|Y)_AXIS_MAX), (0, 0) at center, absloute
                packet.touch1.x = (Int16)(((float)ir.x / 1024 / 1024 - 0.5f) * 1000 * 2);
                packet.touch1.y = (Int16)(((float)ir.y / 1024 / 1024 - 0.5f) * 500 * 2);
            }
            else
            {
                // Increment touch ID on falling edge
                if (client.touching)
                {
                    client.touching = false;
                    client.touchId++;
                }
            }
            
            // Touch2 (ignored)

            // Accel
            if ((flags & Flags.Accel) != 0)
            {
                // UDPWii: In Gs
                // DSU: In Gs
                /*
                // Don't know why but let emulation makes sense
                packet.accel.x = (float)accel.x / 1024 / 1024;
                packet.accel.y = (float)accel.y / 1024 / 1024;
                packet.accel.z = -(float)accel.z / 1024 / 1024;
                */
                // STILL don't know why but let Mario Kart Wii & Wii Sports Resort playable
                // XXX: Why the axises work in this way & how it reacts with JDFix tweak?
                packet.accel.x = (float)accel.x / 1024 / 1024;
                packet.accel.y = -(float)accel.z / 1024 / 1024;
                packet.accel.z = -(float)accel.y / 1024 / 1024;
            }

            // Accel timestamp
            if ((flags & Flags.AccelTS) != 0)
            {
                // UDPWii: In nanoseconds
                // DSU: In nanoseconds
                packet.accel.timestamp = accelTimestamp;
            }

            // Gyro
            if ((flags & Flags.Gyro) != 0)
            {
                client.gyroSeen = true;

                // UDPWii: In rad/s
                // DSU: In deg/s
                // Set after 3 hrs of desperate trial-and-error, tested with Wii Play: Motion & Wii Sports Resort
                // NOTE: Use a phone with hardware gyroscope! Pseudo (virtual) ones are SHITTY and renders emulation unusable
                // XXX: Why the axises work in this way & how it should react with JDFix tweak?
                packet.gyro.pitch = (float)gyro.pitch / 1024 / 1024 * (float)(180 / Math.PI);
                packet.gyro.yaw = -(float)gyro.roll / 1024 / 1024 * (float)(180 / Math.PI);
                packet.gyro.roll = (float)gyro.yaw / 1024 / 1024 * (float)(180 / Math.PI);
            }

            // Slot
            packet.slot = SlotDSU(client);

            // Misc things
            //packet.connected = (byte)((packet.slot.state == DSU.Slot.State.Connected) ? 1 : 0);
            packet.connected = 1; // Always connected (since we're processing a fresh packet)
            packet.packetNumber = client.packetNumber++;

            return packet;
        }
    }

    #endregion
}
