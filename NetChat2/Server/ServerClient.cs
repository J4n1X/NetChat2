using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetChat2
{
    class ServerClient : NetChatClient
    {
        #region Events
        public delegate void OnCommandServerQueryCallback(object sender, string command);
        public event OnCommandServerQueryCallback OnCommandServerQuery;

        public delegate void OnBytesCallback(object sender, byte[] bytes);
        public event OnBytesCallback OnBytes;

        public delegate void OnBroadcastableCallback(object sender, BaseCommand command);
        public event OnBroadcastableCallback OnCommandBroadcastable;

        public delegate void OnDisconnectCallback(object sender);
        public event OnDisconnectCallback OnDisconnect;

        #endregion

        private readonly IPAddress ipAddress;

        private readonly string userName;
        public string UserName { get { return userName; } }

        private Guid serverGuid;

        private Guid guid;
        public Guid Guid { get { return guid; } }

        // Create Client from already connected TcpClient that has also negotiated.
        public ServerClient(Guid clientGuid, string userName, TcpClient client) : base(client)
        {
            this.userName = userName;
            this.guid = clientGuid;
            ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            StartTask();
            available = true;
        }

        // Negotiate with Client, then commence
        public ServerClient(TcpClient client, Guid serverGuid) : base(client)
        {
            this.serverGuid = serverGuid;

            WriteServerCommand(serverGuid, Commands.ServerReady, ("SERVERGUID", serverGuid.ToString()));

            var connectRequest = serializer.Deserialize(Read());
            if (connectRequest.Command != Commands.ClientConnect)
            {
                WriteServerCommand(serverGuid, Commands.ServerInvalidCommand);
                return;
            }
            if (connectRequest.Data["USERNAME"].Length > 255)
            {
                WriteServerCommand(serverGuid, Commands.ServerConnectFailInvalidUserName);
                return;
            }
            this.userName = connectRequest.Data["USERNAME"];
            this.guid = Guid.NewGuid();
            ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            WriteServerCommand(serverGuid, Commands.ServerConnectSuccess, ("CLIENTGUID", guid.ToString()));

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
                        ProcessCommand(Read());
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

        protected override void ProcessClientCommand(BaseCommand command)
        {
            switch (command.Command)
            {
                case Commands.ClientConnect:
                    Write(new BaseCommand(serverGuid, CommandTypes.Server, Commands.ServerInvalidCommand));
                    break;
                case Commands.ClientDisconnect:
                    OnDisconnect?.Invoke(this);

                    break;
                case Commands.ClientText:
                    OnCommandBroadcastable?.Invoke(this, command);
                    break;
            }
        }

        protected override void ProcessServerCommand(BaseCommand command)
        {
            switch (command.Command)
            {
                case Commands.ServerText:
                    OnCommandBroadcastable?.Invoke(this, command);
                    break;
                default:
                    throw new NotImplementedException("A client cannot send server commands");
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
