using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChat2
{
    class Client : NetChatClient
    {
        #region Events
        public delegate void OnServerConnectCallback(object sender, string message);
        public event OnServerConnectCallback OnServerConnect;

        public delegate void OnServerDisconnectCallback(object sender);
        public event OnServerDisconnectCallback OnServerDisconnect;

        public delegate void OnServerConnectFailCallback(object sender, string reason);
        public event OnServerConnectFailCallback OnServerConnectFail;

        public delegate void OnBytesFromServerCallback(object sender, byte[] bytes);
        public event OnBytesFromServerCallback OnBytesFromServer;

        public delegate void OnTextCallback(object sender, BaseCommand command);
        public event OnTextCallback OnText;

        #endregion

        #region Variables

        private Guid clientGuid;
        public Guid ClientGuid { get { return clientGuid; } }

        private Guid serverGuid;

        private readonly IPAddress serverAddress;
        public IPAddress ServerAddress { get { return serverAddress; } }
        private string userName = "";
        public string UserName
        {
            get { return userName; }
            set { if (value.Length <= 64) userName = value; }
        }

        #endregion

        public Client(string peerIp, int port, string userName) : base() 
        {
            localAddress = GetLocalIPAddress();
            serverAddress = IPAddress.Parse(peerIp);
            this.port = port;
            this.userName = userName;
            serializer = new Serializer();
        }

        public void Connect()
        {
            if (!client.Connected)
                base.Open(serverAddress.ToString(), port);
            var readyCommand = serializer.Deserialize(Encoding.ASCII.GetString(Read()));
            if (readyCommand.Command == Commands.ServerReady)
            {
                serverGuid = Guid.Parse(readyCommand.Data["SERVERGUID"]);
                WriteClientCommand(clientGuid, Commands.ClientConnect, ("USERNAME", userName));
                var result = serializer.Deserialize(Encoding.ASCII.GetString(Read()));
                if (result.Command == Commands.ServerConnectFailInvalidUserName)
                    throw new ArgumentException("The Username specified is invalid!");
                else if (result.Command == Commands.ServerInvalidCommand)
                    throw new Exception("An invalid command has been sent to the server");
                else
                    clientGuid = Guid.Parse(result.Data["CLIENTGUID"]);

                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.CurrentThread.Name = "Client Thread";
                    while (!disposing)
                        Update();
                });

                string connectMessage = String.Format(
                                "Successfully connected to {0}!\nGUID: {1}\nUsername: {2}\n",
                                serverAddress,
                                clientGuid,
                                UserName
                            );
                OnServerConnect?.Invoke(this, connectMessage);
                available = true;

            }
            else
            {
                throw new Exception("The server sent a different message than \"SERVER_READY\"");
            }
        }

        private void Update()
        {
            try
            {
                ProcessCommand(Read());
            }
            catch (System.IO.IOException)
            {
                // Simply close client
                Dispose();
            }
        }

        protected override void ProcessClientCommand(BaseCommand command)
        {
            switch (command.Command)
            {
                case Commands.ClientConnect:
                case Commands.ClientDisconnect:
                    break;
                case Commands.ClientText:
                    OnText(this, command);
                    break;
            }
        }

        protected override void ProcessServerCommand(BaseCommand command)
        {
            switch (command.Command)
            {
                case Commands.ServerText:
                    OnText?.Invoke(this, command);
                    break;
                case Commands.ServerClosing:
                    OnServerDisconnect?.Invoke(this);
                    break;
                default:
                    throw new NotImplementedException("A client cannot send server commands");
            }
            
        }



        // sends text to server
        public void SendText(string text)
        {
            if (!available) 
                return;

            //if (flags != TextFlags.NoUserName)
            //    text = '<' + userName + "> " + text;
            var args = new Dictionary<string, string>()
            {
                {"USERNAME", userName },
                {"TEXT", text }
            };
            WriteClientCommand(clientGuid, Commands.ClientText, args);
        }

        public void Disconnect()
        {
            if (!client.Connected)
                return;
            WriteClientCommand(clientGuid, Commands.ClientDisconnect);
            available = false;
        }

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public new void Dispose()
        {
            if (client != null)
            {
                if (available)
                {
                    Disconnect();
                    base.Dispose();
                    available = false;
                }
            }
        }

        ~Client()
        {
            Dispose();
        }
    }
}