using System;
using System.Runtime.InteropServices;

namespace UDPWiiHook.DSU
{
    /*
        NOTE: Many things are different between v1993/cemuhook-protocol and Dolphin's implementation
            For the sake of HISTORY BURDEN, fields' names are from cemuhook-protocol,
            but type, meaning etc. are revised to be compatible with Dolphin.
    */
    /*
        NOTE: Dolphin does not implement many of the buttons
            dolphin@6042df7:/Source/Core/InputCommon/ControllerInterface/DualShockUDPClient/DualShockUDPClient.cpp#L509
        All the things implemented in Dolphin:
            Analog.*, Buttons.{L3, R3, Share, Options}, ButtonPS, ButtonTouch,
            Stick.*, Touch1.*, Accel.{x, y, z}, Gyro.*
    */

    #region Header & Serialization routines

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Header
    {
        #region Inner types

        public enum Magic : UInt32
        {
            DSUC = 0x43555344,
            DSUS = 0x53555344
        }

        public enum PacketType : UInt32
        {
            Version = 0x100000,
            Slot = 0x100001,
            Data = 0x100002
        }

        #endregion

        public const UInt16 VERSION = 1001;

        public readonly Magic magic;
        private UInt16 version;
        private UInt16 length;
        private readonly UInt32 crc32 = 0; // Placeholder only
        private UInt32 id;
        public PacketType type;

        internal Header() { }

        public Header(Type clazz, Magic magic, PacketType type, UInt32 id)
        {
            this.magic = magic;
            this.version = VERSION;
            this.length = (UInt16)(Marshal.SizeOf(clazz) - Marshal.SizeOf(typeof(Header)));
            this.id = id;
            this.type = type;
        }

        public byte[] Finish()
        {
            int realLength = this.length + Marshal.SizeOf(typeof(Header));

            byte[] data = new byte[realLength];
            IntPtr pData = Marshal.AllocHGlobal(realLength);

            Marshal.StructureToPtr(this, pData, true);
            Marshal.Copy(pData, data, 0, realLength);
            Marshal.FreeHGlobal(pData);

            UInt32 crc32 = Crc32.Get(data);
            Array.Copy(BitConverter.GetBytes(crc32), 0, data, (int)Marshal.OffsetOf(typeof(Header), "crc32"), sizeof(UInt32));

            return data;
        }

        public bool IsValid(byte[] packet)
        {
            // 1. Magic number
            if (!Enum.IsDefined(typeof(Magic), this.magic))
                return false;

            // 2. Version
            if (this.version > VERSION)
                return false;

            // 3. Type
            if (!Enum.IsDefined(typeof(PacketType), this.type))
                return false;

            // 4. CRC32
            byte[] data = (byte[])packet.Clone();
            Array.Clear(data, (int)Marshal.OffsetOf(typeof(Header), "crc32"), sizeof(UInt32));

            return Crc32.Get(data) == this.crc32;
        }
    }

    #endregion

    #region Version

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class VersionReq : Header
    {
        private VersionReq() { }

        public VersionReq(UInt32 id)
            : base(typeof(VersionReq), Header.Magic.DSUC, Header.PacketType.Version, id) { }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class VersionRsp : Header
    {
        public UInt16 maxVersion;

        private VersionRsp() { }

        public VersionRsp(UInt32 id, UInt16 maxVersion = Header.VERSION)
            : base(typeof(VersionRsp), Header.Magic.DSUS, Header.PacketType.Version, id)
        {
            this.maxVersion = maxVersion;
        }
    }

    #endregion

    #region Slot

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Slot
    {
        #region Inner types

        public enum State : Byte
        {
            NotConnected = 0,
            Reserved = 1,
            Connected = 2
        }

        public enum Model : Byte
        {
            NotApplicable = 0, // None
            PartialGyro = 1,   // DS3 = DualShock 3
            FullGyro = 2,      // DS4 = DualShock 4
            VR = 3             // Generic = Generic Gamepad
        }

        public enum Connection : Byte
        {
            NotApplicable = 0,
            USB = 1,
            Bluetooth = 2
        }

        public enum Battery : Byte
        {
            NotApplicable = 0,
            Dying = 1,
            Low = 2,
            Medium = 3,
            High = 4,
            Full = 5,
            Charging = 0xee,
            Charged = 0xef
        }

        #endregion

        public Byte slot;
        public State state;
        public Model model;
        public Connection connection;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public Byte[] mac;
        public Battery battery;
    }

    // NOTE: This struct is ill-defined and for deserialzaion only
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SlotReq : Header
    {
        public Int32 count;

        // NOTE: Leftover data
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 0)] public Byte[] slots;

        private SlotReq() { }

        public SlotReq(UInt32 id)
            : base(typeof(SlotReq), Header.Magic.DSUC, Header.PacketType.Slot, id) { }

        public byte[] GetLeftoverData(byte[] packet)
        {
            int length = packet.Length - Marshal.SizeOf(this);
            if (length <= 0)
                return new byte[0];

            byte[] data = new byte[length];
            Array.Copy(packet, Marshal.SizeOf(this), data, 0, length);
            return data;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SlotRsp : Header
    {
        public Slot slot;
        private readonly Byte endByte = 0;

        private SlotRsp()
        {
            this.slot.mac = new Byte[6];
        }

        public SlotRsp(UInt32 id)
            : base(typeof(SlotRsp), Header.Magic.DSUS, Header.PacketType.Slot, id)
        {
            this.slot.mac = new Byte[6];
        }
    }

    #endregion

    #region Data

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DataReq : Header
    {
        #region Inner types

        public enum Action : Byte
        {
            SubscribeAll = 0,
            SubscribeSlot = 1,
            SubscribeMAC = 2
        }

        #endregion

        public Action action;
        public Byte slot;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public Byte[] mac;

        private DataReq()
        {
            this.mac = new Byte[6];
        }

        public DataReq(UInt32 id)
            : base(typeof(DataReq), Header.Magic.DSUC, Header.PacketType.Data, id)
        {
            this.mac = new Byte[6];
        }
}

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class DataRsp : Header
    {
        #region Inner types

        // NOTE: 2 bytes of buttons are combined
        [Flags]
        public enum Buttons : UInt16
        {
            None = 0,

            DPadLeft  = 1 << 7,
            DPadDown  = 1 << 6,
            DPadRight = 1 << 5,
            DPadUp    = 1 << 4,
            Options   = 1 << 3,
            R3        = 1 << 2,
            L3        = 1 << 1,
            Share     = 1 << 0,

            Y  = 0x100 << 7,
            B  = 0x100 << 6,
            A  = 0x100 << 5,
            X  = 0x100 << 4,
            R1 = 0x100 << 3,
            L1 = 0x100 << 2,
            R2 = 0x100 << 1,
            L2 = 0x100 << 0
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Stick
        {
            public Byte leftX;
            public Byte leftY; // Inverted
            public Byte rightX;
            public Byte rightY; // Inverted
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Analog
        {
            public Byte DPadLeft;
            public Byte DPadDown;
            public Byte DPadRight;
            public Byte DPadUp;
            public Byte Y; // Square
            public Byte B; // Cross
            public Byte A; // Circle
            public Byte X; // Triangle
            public Byte R1;
            public Byte L1;
            public Byte R2;
            public Byte L2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Touch
        {
            public Byte active;
            public Byte id;
            public Int16 x;
            public Int16 y;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Accel
        {
            public UInt64 timestamp; // In us
            public Single x; // In Gs
            public Single y; // In Gs
            public Single z; // In Gs
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Gyro
        {
            public Single pitch; // In deg/s
            public Single yaw; // In deg/s
            public Single roll; // In deg/s
        }

        #endregion

        public Slot slot;
        public Byte connected;
        public UInt32 packetNumber;
        public Buttons buttons;
        public Byte buttonPS;
        public Byte buttonTouch;
        public Stick stick;
        public Analog analog;
        public Touch touch1;
        public Touch touch2;
        public Accel accel;
        public Gyro gyro;

        private DataRsp()
        {
            this.slot.mac = new Byte[6];
        }

        public DataRsp(UInt32 id)
            : base(typeof(DataRsp), Header.Magic.DSUS, Header.PacketType.Data, id)
        {
            this.slot.mac = new Byte[6];
        }
}

    #endregion
}
