using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetChat2
{
    class ServerClient : NetChatClient
    {
        #region Events
        public delegate void OnAdvancedCommandCallBack(object sender, string command);
        public event OnAdvancedCommandCallBack OnAdvancedCommand;

        public delegate void OnBytesCallback(object sender, byte[] bytes);
        public event OnBytesCallback OnBytes;

        public delegate void OnTextCallback(object sender, string text, TextFlags flags);
        public event OnTextCallback OnText;

        public delegate void OnDisconnectCallback(object sender);
        public event OnDisconnectCallback OnDisconnect;

        #endregion

        private IPAddress ipAddress;

        private string userName;
        public string UserName { get { return userName; } }

        private Guid guid;
        public Guid Guid { get { return guid; } }

        // Create Client from already connected TcpClient that has also negotiated.
        public ServerClient(Guid guid, string userName, TcpClient client) : base(client)
        {
            this.userName = userName;
            this.guid = guid;
            ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            StartTask();
            available = true;
        }

        // Negotiate with Client, then commence
        public ServerClient(TcpClient client) : base(client)
        {
            Write(ServerFlags.Ready);
            var connectRequest = Read();
            if ((ClientFlags)connectRequest.Flag != ClientFlags.Connect)
            {
                Write(ServerFlags.InvalidCommand);
                return;
            }
            if (connectRequest.Size > 255)
            {
                Write(ServerFlags.InvalidUserName);
                return;
            }
            this.userName = GetText(connectRequest.Data, TextFlags.Unicode);
            this.guid = Guid.NewGuid();
            ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            Write(ServerFlags.ConnectSuccess, guid.ToByteArray());
            
            StartTask();
            available = true;
        }

        private void StartTask()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.CurrentThread.Name = "Server-Client Thread";
                while (!disposing)
                {
                    try
                    {
                        ReadClient(Read());
                    }
                    catch
                    {
                        break;
                    }
                }
                base.Dispose();
                available = false;
            });
        }

        private void ReadClient(Packet command)
        {
            try
            {
                switch (command.Flag & 0b00001111) 
                {
                    case (byte)TextFlags.Base: // Text
                        ReadClientText(command);
                        break;
                    case (byte)DataFlags.Base: // Data
                        break;
                    case (byte)ClientFlags.Base:
                        ReadClientCommand(command);
                        break;
                    case (byte)ServerFlags.Base: // Server responses
                        break;
                    case (byte)AdvancedFlags.Base: // Advanced Server Command
                        // Jump back to server in order to handle execution
                        OnAdvancedCommand(this, GetText(command.Data, TextFlags.Ascii));
                        break;
                } // Server responses
            }
            catch
            {
                return;
            }
        }

        private void ReadClientText(Packet command)
        {
            var textFlag = (TextFlags)command.Flag;

            // decode so we can do things like filter text later
            string message = GetText(command.Data, textFlag);
            if (textFlag != TextFlags.NoUserName)
                message = "<" + userName + "> " + message;

            if (OnText != null) OnText(this, message, textFlag);
        }

        private void ReadClientCommand(Packet command)
        {
            switch ((ClientFlags)command.Flag)
            {
                case ClientFlags.Connect:
                    break;
                case ClientFlags.Disconnect:
                    if (OnDisconnect != null) OnDisconnect(this);
                    break;
                case ClientFlags.Extended:
                    break;
            }
        }

        public new void Dispose()
        {
            disposing = true;
        }

        ~ServerClient()
        {
            Dispose();
        }
    }
}
