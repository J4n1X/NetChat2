using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;
using System.Linq;

namespace NetChat2
{

    internal abstract class NetChatClient : ChatBase
    {
        /// <summary>
        /// Deprecated.
        /// </summary>
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
        protected TcpClient client;

        public NetChatClient()
        {
            client = new TcpClient();
            available = false;
        }

        public NetChatClient(TcpClient client)
        {
            this.client = client;
            JsonSerializer = new Serializer();
            this.stream = this.client.GetStream();
            this.available = true;
        }

        public void Open(string peerIp, int port)
        {
            client ??= new TcpClient();
            try
            {
                client.ConnectAsync(peerIp, port).Wait(TimeSpan.FromSeconds(10));
                stream = client.GetStream();
            }
            catch (Exception e) // if the connection fails, then the server will be used
            {
                throw e;
            }
            this.available = true;
        }

        public byte[] Read()
        {
            if (!available)
                throw new System.IO.IOException("Client is not available");

            try
            {
                byte[] buffer = new byte[4];
                stream.Read(buffer, 0, buffer.Length);
                byte[] commandBytes = new byte[BitConverter.ToUInt32(buffer)];
                if (commandBytes.Length > 0)
                    stream.Read(commandBytes, 0, commandBytes.Length);
                return commandBytes;
            }
            catch(ObjectDisposedException ex)
            {
                if(disposing) return null;
                throw ex;
            }
        }

        public void ProcessCommand(byte[] data)
        {
            try
            {
                var commandString = Encoding.ASCII.GetString(data);
                var command = JsonSerializer.Deserialize<CommandObject>(commandString);

                // Uncomment for a communication-dump after the connection has been estabilished.
                //File.AppendAllText("communication.txt", Newtonsoft.Json.JsonConvert.SerializeObject(command, Newtonsoft.Json.Formatting.Indented) + "\n");

                switch (command.Type)
                {
                    case CommandTypes.Client:
                        ProcessClientCommand(command);
                        break;
                    case CommandTypes.Server:
                        ProcessServerCommand(command);
                        break;
                }
            }
            catch
            {
                return;
            }
        }

        protected abstract void ProcessClientCommand(CommandObject command);
        protected abstract void ProcessServerCommand(CommandObject command);

        public void WriteServerCommand(Guid serverGuid, BaseCommand command, params ValueTuple<string, string>[] data)
        {
            var args = new Dictionary<string, string>();
            WriteServerCommand(serverGuid, command, data.ToDictionary(x => x.Item1, x => x.Item2));
        }

        public void WriteClientCommand(Guid clientGuid, BaseCommand command, params ValueTuple<string, string>[] data)
        {
            var args = new Dictionary<string, string>();
            WriteClientCommand(clientGuid, command, data.ToDictionary(x => x.Item1, x => x.Item2));
        }

        public void WriteClientCommand(Guid clientGuid, BaseCommand command, Dictionary<string, string> data = null)
        {
            Write(new CommandObject(clientGuid, CommandTypes.Client, command, data));
        }

        public void WriteServerCommand(Guid serverGuid, BaseCommand command, Dictionary<string, string> data = null)
        {
            Write(new CommandObject(serverGuid, CommandTypes.Server, command, data));
        }

        public void Write(CommandObject command) => Write(Encoding.ASCII.GetBytes(JsonSerializer.Serialize(command)));

        public void Write(byte[] data)
        {
            if (!available)
                throw new System.IO.IOException("Client is not available");
            if (!client.Connected)
                throw new ArgumentException("Client is not connected.");

            data ??= new byte[0];
            if (!client.Connected)
                return;
            var stream = client.GetStream();
            try
            {
                stream.Write(BitConverter.GetBytes(data.Length));
                stream.Write(data);
            }
            catch (Exception)
            {
                this.available = false;
            }
        }

        public new void Dispose()
        {
            if (!disposing)
            {
                GC.SuppressFinalize(this);
                base.Dispose(); // turn off threads
                stream.Close();
                client.Close();
            }
        }
        
        ~NetChatClient()
        {
            Dispose();
        }
    }
}
