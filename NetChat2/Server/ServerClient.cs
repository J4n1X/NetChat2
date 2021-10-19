using System;
using System.Collections.Generic;
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
            var serverVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.serverGuid = serverGuid;

            WriteServerCommand(
                serverGuid, 
                BaseCommand.SERVER_READY, 
                ("SERVERGUID", serverGuid.ToString()),
                ("VERSION", serverVersion.ToString()));

            var connectRequest = JsonSerializer.Deserialize<CommandObject>(Read());
            if (connectRequest.Command != BaseCommand.CLIENT_CONNECT)
            {
                if(connectRequest.Command == BaseCommand.CLIENT_ABORT)
                {
                    client.Close();
                    available = false;
                    return;
                }

                WriteServerCommand(
                    serverGuid, 
                    BaseCommand.SERVER_INVALID_COMMAND, 
                    ("REASON", Errors.Reasons.INVALID_COMMAND_CONTEXT.ToString())
                    );
                return;
            }

            // this should never be false, since the client checks for the server version
            if(Version.Parse(connectRequest.Data["VERSION"]) != serverVersion)
            {
                WriteServerCommand(serverGuid, BaseCommand.SERVER_CONNECTION_FAILED, ("REASON", Errors.Reasons.VERSION_MISMATCH.ToString()));
                return;
            }

            if (connectRequest.Data["USERNAME"].Length > 255)
            {
                WriteServerCommand(serverGuid, BaseCommand.SERVER_CONNECTION_FAILED, ("REASON", Errors.Reasons.INVALID_USERNAME.ToString()));
                return;
            }
            this.userName = connectRequest.Data["USERNAME"];
            this.guid = Guid.NewGuid();
            ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

            WriteServerCommand(serverGuid, BaseCommand.SERVER_CONNECT_SUCCESS, ("CLIENTGUID", guid.ToString()));

            StartTask();

            Server.BroadcastServerCommand(BaseCommand.SERVER_TEXT, ("TEXT", userName + " has connected!\n"));

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

        protected override void ProcessClientCommand(CommandObject command)
        {
            switch (command.Command)
            {
                case BaseCommand.CLIENT_CONNECT:
                    Write(new CommandObject(serverGuid, CommandTypes.Server, BaseCommand.SERVER_INVALID_COMMAND));
                    break;
                case BaseCommand.CLIENT_DISCONNECT:
                    Dispose();
                    break;
                case BaseCommand.CLIENT_TEXT:
                    command.Data.Add("USERNAME", Server.Clients[command.SenderGuid].UserName);
                    Server.BroadcastCommand(command);
                    break;
                case BaseCommand.CLIENT_ADVANCEDCOMMAND:
                    ProcessAdvancedClientCommand(command);
                    break;
            }
        }

        private void ProcessAdvancedClientCommand(CommandObject command)
        {
            if(Enum.TryParse(command.Data["COMMAND"] ,out AdvancedClientCommand commandEnum))
            {
                switch (commandEnum)
                {
                    case AdvancedClientCommand.GETUSERUUIDS:
                        
                        break;
                    case AdvancedClientCommand.WHISPER:
                        AdvancedCommands.Server.Whisper(command);
                        break;
                }
            }
            else
            {
                WriteServerCommand(
                    serverGuid, 
                    BaseCommand.SERVER_INVALID_COMMAND,
                    ("REASON", Errors.Reasons.INVALID_COMMAND.ToString())
                    );
            }
        }

        protected override void ProcessServerCommand(CommandObject command)
        {
            switch (command.Command)
            {
                case BaseCommand.SERVER_TEXT:
                    Server.BroadcastCommand(command);
                    break;
                default:
                    throw new NotImplementedException("A client cannot send server commands");
            }
        }


        

        public new void Dispose()
        {
            disposing = true;
            Server.BroadcastServerCommand(BaseCommand.SERVER_TEXT, ("TEXT", userName + " has disconnected!\n"));
            OnDisconnect?.Invoke(this);
        }

        ~ServerClient()
        {
            Dispose();
        }
    }
}
