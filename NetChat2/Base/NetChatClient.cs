using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace NetChat2
{
    internal class NetChatClient : ChatBase
    {
        public sealed class Packet
        {
            private byte flag;
            public byte Flag
            {
                get => flag;
                set => flag = value;
            }

            private ushort size;
            /// <summary>
            /// Setting DataSizeByte will also initialize the Data array to the size passed as ushort
            /// </summary>
            public ushort Size
            {
                get => size;
                set
                {
                    size = value;
                    // initialize Data Array
                    Data = new byte[size];
                }
            }
            public byte[] GetDataSizeBytes() => BitConverter.GetBytes(Size);
            public void SetDataSizeFromBytes(byte[] bytes) => Size = BitConverter.ToUInt16(bytes);

            private byte[] data;
            public byte[] Data
            {
                get
                {
                    if(this.data.Length == this.Size)
                    {
                        return data;
                    }
                    else
                    {
                        this.data = new byte[this.Size];
                        return this.Data;
                    }    
                }
                set => data = value;
            }

            public Packet()
            {
                data = null;
            }
        }

        private NetworkStream stream;
        private TcpClient client;

        public NetChatClient(TcpClient client)
        {
            if (!client.Connected)
                throw new ArgumentException("Client is not connected.");
            this.client = client;
            this.stream = this.client.GetStream();
            this.available = true;
        }

        public Packet Read()
        {
            var retPacket = new Packet();
            retPacket.Flag = (byte)stream.ReadByte();
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, buffer.Length);
            retPacket.SetDataSizeFromBytes(buffer);
            if(retPacket.Size > 0)
                stream.Read(retPacket.Data, 0, retPacket.Data.Length);
            return retPacket;
        }


        public void Write(TextFlags flags, byte[] data = null) => Write((byte)flags, data);

        public void Write(DataFlags flags, byte[] data = null) => Write((byte)flags, data);
        public void Write(ClientFlags flags, byte[] data = null) => Write((byte)flags, data);

        public void Write(ServerFlags flags, byte[] data = null) => Write((byte)flags, data);
        public void Write(AdvancedFlags flags, byte[] data = null) => Write((byte)flags, data);

        public void Write(byte flags, byte[] data = null)
        {
            data = data ?? new byte[0];
            if (!client.Connected)
                return;
            int cycles = data.Length / ushort.MaxValue;
            var stream = client.GetStream();

            stream.WriteByte(flags);
            for (int i = 0; i < cycles; i++)
            {
                stream.Write(BitConverter.GetBytes(ushort.MaxValue), 0, 2);
                stream.Write(data[(i * ushort.MaxValue)..((i + 1) * ushort.MaxValue)], 0, ushort.MaxValue);
            }
            int finalLength = (data.Length - (ushort.MaxValue * cycles));
            stream.Write(BitConverter.GetBytes((ushort)finalLength), 0, 2);
            stream.Write(data[(cycles * ushort.MaxValue)..data.Length], 0, finalLength);
        }

        public new void Dispose()
        {
            this.client.Close();
        }
        ~NetChatClient()
        {
            this.client.Close();
        }
    }
}
