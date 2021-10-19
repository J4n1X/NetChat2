using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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

        public delegate void OnUserMessageCallback(object sender, CommandObject command);
        public event OnUserMessageCallback OnText;

        public delegate void OnCommandTextResponseCallback(string text);
        public event OnCommandTextResponseCallback OnCommandTextResponse;

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
            JsonSerializer = new Serializer();
        }

        public void Connect()
        {
            if (!client.Connected)
                base.Open(serverAddress.ToString(), port);
            var readyCommand = JsonSerializer.Deserialize<CommandObject>(Encoding.ASCII.GetString(Read()));
            if (readyCommand.Command == BaseCommand.SERVER_READY)
            {
                serverGuid = Guid.Parse(readyCommand.Data["SERVERGUID"]);
                var serverVersion = Version.Parse(readyCommand.Data["VERSION"]);
                var clientVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (clientVersion != serverVersion)
                    throw new InvalidVersionException(
                        clientVersion,
                        serverVersion,
                        $"Invalid Server version {readyCommand.Data["VERSION"]}, Client version is {clientVersion}"
                    );

                WriteClientCommand(
                    clientGuid,
                    BaseCommand.CLIENT_CONNECT,
                    ("USERNAME", userName),
                    ("VERSION", clientVersion.ToString())
                    );
                var result = JsonSerializer.Deserialize<CommandObject>(Encoding.ASCII.GetString(Read()));
                if (result.Command == BaseCommand.SERVER_CONNECTION_FAILED)
                {
                    var reason = (Errors.Reasons)Enum.Parse(typeof(Errors.Reasons), result.Data["REASON"]);
                    if (reason == Errors.Reasons.INVALID_USERNAME)
                    {
                        throw new ArgumentException("The Username specified is invalid!");
                    }
                    else if (reason == Errors.Reasons.VERSION_MISMATCH)
                    {
                        throw new InvalidVersionException(
                            clientVersion,
                            serverVersion,
                            $"Server reports invalid client version {clientVersion}, Server version is {serverVersion}"
                        );
                    }
                }
                else if (result.Command == BaseCommand.SERVER_INVALID_COMMAND)
                    throw new InvalidCommandException(
                        "An invalid command has been sent to the Server.\n" +
                        $"Command sent: {BaseCommand.CLIENT_CONNECT} with data {("USERNAME", userName)}\n" +
                        $"Server reported reason: {Errors.ReasonText[result.Data["REASON"]]}"
                        );
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

        protected override void ProcessClientCommand(CommandObject command)
        {
            switch (command.Command)
            {
                case BaseCommand.CLIENT_CONNECT:
                case BaseCommand.CLIENT_DISCONNECT:
                    break;
                case BaseCommand.CLIENT_TEXT:
                    OnText(this, command);
                    break;
            }
        }

        protected override void ProcessServerCommand(CommandObject command)
        {
            switch (command.Command)
            {
                case BaseCommand.SERVER_TEXT:
                    OnCommandTextResponse?.Invoke(command.Data["TEXT"]);
                    break;
                case BaseCommand.SERVER_CLOSING:
                    OnServerDisconnect?.Invoke(this);
                    break;
                case BaseCommand.SERVER_INVALID_COMMAND:
                    OnCommandTextResponse?.Invoke(Errors.ReasonText[command.Data["REASON"]] + '\n');
                    break;
                case BaseCommand.SERVER_ADVANCEDCOMMANDRESULT:
                    ProcessClientAdvancedCommandResponse(command);
                    break;
                default:
                    throw new NotImplementedException("A client cannot send server commands");
            }

        }

        private void ProcessClientAdvancedCommandResponse(CommandObject command)
        {
            if (Enum.TryParse(command.Data["COMMAND"].ToUpper(), out AdvancedClientCommand commandEnum))
            {
                switch(commandEnum)
                {
                    case AdvancedClientCommand.GETUSERUUIDS:
                        OnCommandTextResponse(AdvancedCommands.Client.Processing.FormatUserUUIDs(command));
                        break;
                    case AdvancedClientCommand.WHISPER:
                        OnCommandTextResponse(AdvancedCommands.Client.Processing.FormatWhisper(command));
                        break;
                }
                return;
            }
        }



        // sends text to server
        public void SendText(string text)
        {
            if (!available)
                return;

            //if (flags != TextFlags.NoUserName)
            //    text = '<' + userName + "> " + text;
            WriteClientCommand(
                clientGuid,
                BaseCommand.CLIENT_TEXT,
                ("TEXT", text)
                );
        }

        public void SendCommand(string text)
        {
            if(text.Contains('(') && text.Contains(')'))
            {
                string command = text[1..text.IndexOf('(')]; // The Command itself
                string[] commandArgs = text[(text.IndexOf('(') + 1)..text.LastIndexOf(')')].Split(','); // Arguments between the ( )

                // if this is false, then just send a normal message instead
                if (Enum.TryParse(command.ToUpper(), out AdvancedClientCommand commandEnum))
                {
                    WriteClientCommand(
                    clientGuid,
                    BaseCommand.CLIENT_ADVANCEDCOMMAND,
                    ("COMMAND", commandEnum.ToString()),
                    ("ARGS", JsonSerializer.Serialize(commandArgs))
                    );
                    return;
                }
            }
            else
            {
                SendText(text + '\n');
            }
        }

        public void Disconnect()
        {
            if (!client.Connected)
                return;
            WriteClientCommand(clientGuid, BaseCommand.CLIENT_DISCONNECT);
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