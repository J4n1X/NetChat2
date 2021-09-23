using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetChat2
{
    class Client : ChatBase
    {
        #region Events
        public delegate void OnServerConnectCallback(object sender, string message);
        public event OnServerConnectCallback OnServerConnect;

        public delegate void OnServerDisconnectCallback(object sender, string message);
        public event OnServerDisconnectCallback OnServerDisconnect;

        public delegate void OnServerConnectFailCallback(object sender, string reason);
        public event OnServerConnectFailCallback OnServerConnectFail;

        public delegate void OnBytesFromServerCallback(object sender, byte[] bytes);
        public event OnBytesFromServerCallback OnBytesFromServer;

        public delegate void OnTextFromServerCallback(object sender, string text);
        public event OnTextFromServerCallback OnTextFromServer;

        #endregion

        #region Variables

        public NetChatClient client;

        private CancellationTokenSource clientToken = new CancellationTokenSource();

        private Guid clientID;
        public Guid ClientID { get { return clientID; } }
        private Server localServer = new Server();
        public Server LocalServer { get { return localServer; } }
        private IPAddress serverAddress;
        public IPAddress ServerAddress { get { return serverAddress; } }
        private string userName = "";
        public string UserName
        {
            get { return userName; }
            set { if (value.Length <= 64) userName = value; }
        }

        #endregion

        public void StartServer(string localIP, int port)
        {
            if (available)
                return;
            
            localServer.Listen(localIP, port);
        }

        public void Connect(string peerIp, int port, string userName)
        {
            if (!available)
            {
                localAddress = GetLocalIPAddress();
                serverAddress = IPAddress.Parse(peerIp);
                this.port = port;
                this.userName = userName;
                var newClient = new TcpClient();
                try
                {
                    try
                    {
                        newClient.ConnectAsync(peerIp, port).Wait(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception e) // if the connection fails, then the server will be used
                    {
                        OnServerConnectFail(this, $"Connection failed with this error:\n{e.ToString()}");
                        return;
                    }

                    if (newClient.Connected)
                    {
                        client = new NetChatClient(newClient);

                        if (((ServerFlags)client.Read().Flag != ServerFlags.Ready))
                            return;

                        client.Write(ClientFlags.Connect, GetTextBytes(userName, TextFlags.Unicode));

                        //byte[] buffer = GetTextBytes(userName, TextFlags.Unicode);
                        //clientStream.WriteByte((byte)ClientFlags.Connect);
                        //clientStream.WriteByte((byte)buffer.Length);
                        //clientStream.Write(buffer, 0, buffer.Length);
                        //ServerFlags response = (ServerFlags)clientStream.ReadByte();
                        var response = client.Read();
                        if ((ServerFlags)response.Flag == ServerFlags.ConnectSuccess)
                        {
                            if (response.Size == 16)
                                clientID = new Guid(response.Data);
                            else
                                throw new DataMisalignedException();
                            string connectMessage = String.Format(
                                "Successfully connected to {0}!\nGUID: {1}\nUsername: {2}\n",
                                serverAddress,
                                clientID,
                                UserName
                            );

                            Task.Run(() =>
                            {
                                Thread.CurrentThread.Name = "Client Thread";
                                while (!needToCleanUp)
                                    Update();
                            },
                            clientToken.Token);
                            
                            if (OnServerConnect != null) OnServerConnect(this, connectMessage);
                        }
                        else
                        {
                            if(OnServerConnectFail != null) OnServerConnectFail(this, "The Username specified is invalid or too long.");
                            return;
                        }

                        available = true;
                    }
                    else
                    {
                        if(OnServerConnectFail != null) OnServerConnectFail(this, "Connection failed because of an unknown error.");
                        return;
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
                    throw;
                }
            }
        }

        private void Update()
        {
            try
            {
                var command = client.Read();

                if ((command.Flag & 0b00001111) == (byte)TextFlags.Base) // Text
                {
                    // decode so we can do things like filter text later
                    string message = GetText(command.Data, (TextFlags)command.Flag);
                    if (OnTextFromServer != null) OnTextFromServer(this, message);
                }
                else if ((command.Flag & 0b00001111) == (byte)DataFlags.Base) ; // Data
                else if ((command.Flag & 0b00001111) == (byte)ServerFlags.Base) ; // Server responses
            }
            catch (System.IO.IOException e)
            {
                Disconnect();
                if(OnServerDisconnect != null) OnServerDisconnect(this, "Server closed connection");
                available = false;
            }
        }


        // sends text to server
        public void SendText(string text, TextFlags flags)
        {
            if (!available) 
                return;

            //if (flags != TextFlags.NoUserName)
            //    text = '<' + userName + "> " + text;
           client.Write(TextFlags.Unicode, GetTextBytes(text, TextFlags.Unicode));

        }

        public void Disconnect()
        {
            if (!available)
                return;
            client.Write(ClientFlags.Disconnect);
            clientToken.Cancel();
            client.Dispose();
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
            localServer.Dispose();
            if (client != null)
            {
                if (client.Available)
                {
                    Disconnect();
                    client.Dispose();
                }
            }
            needToCleanUp = true;
        }

        ~Client()
        {
            if(localServer.Available)
                localServer.Dispose();
            Dispose();
        }
    }
}