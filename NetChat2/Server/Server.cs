using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace NetChat2
{
    // This is both a server and a client, as it's used by the form.
    class Server : ChatBase
    {
        #region Events
        public delegate void OnClientConnectCallback(object sender);
        public event OnClientConnectCallback OnClientConnect;
        #endregion

        #region Variables

        TcpListener listener;
        CancellationTokenSource listenerToken = new CancellationTokenSource();

        Dictionary<Guid, ServerClient> clients = new Dictionary<Guid, ServerClient>();

        #endregion

        public Server() { }
        public Server(string localIP, int port = 9000) => Listen(localIP, port);

        public void Listen(string localIP, int port = 9000)
        {
            if (!available)
            {
                this.port = port;
                localAddress = IPAddress.Parse(localIP);
                listener = new TcpListener(IPAddress.Any, this.port);

                listener.Start();
                
                ThreadPool.QueueUserWorkItem(async delegate
                {
                    Thread.CurrentThread.Name = "Listener Thread";
                    try
                    {
                        while (!disposing)
                        {
                            listenerToken.Token.ThrowIfCancellationRequested();
                            ProcessNewClient(await listener.AcceptTcpClientAsync());
                        }
                    }
                    finally
                    {
                        listener.Stop();
                    }
                });
            }
            this.available = true;
        }

        private void ProcessNewClient(TcpClient client)
        {
            try
            {
                var newClient = new ServerClient(client);
                if(newClient.Available)
                newClient.OnText += ClientTextReceived;
                newClient.OnDisconnect += ClientDisconnect;
                newClient.OnAdvancedCommand += ProcessAdvancedCommand;
                clients.Add(newClient.Guid, newClient);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                throw;
            }
        }

        
        private void ProcessAdvancedCommand(object sender, string command)
        {
            switch (command.ToUpper())
            {
                case "GETUSERGUIDTABLE":
                    var userNames = new List<byte>();
                        ((ServerClient)sender).Write(AdvancedFlags.ServerRequest,
                        AdvancedCommands.GetUsernameGuidTable(
                            clients.Keys.ToArray(),
                            clients.Values.Select(x => x.UserName).ToArray()
                            )
                        ); 
                    break;
            }

        }

        private void ClientTextReceived(object sender, string text, TextFlags flags)
        {
            if (!Available)
                return;
            SendText(text, flags);               
        }

        private void SendText(string text, TextFlags flags = (TextFlags.Unicode | TextFlags.NoUserName))
        {
            if (!Available)
                return;
            
            byte[] messageBytes = GetTextBytes(text, flags);
            foreach (var client in clients.Values)
            {
                client.Write(flags, messageBytes);
            }
        }

        private void ClientDisconnect(object sender)
        {

            var client = ((ServerClient)sender);
            clients.Remove(((ServerClient)sender).Guid);
            SendText(client.UserName + " has disconnected!\n", TextFlags.Unicode | TextFlags.NoUserName);
            client.Dispose();
        }

        public new void Dispose()
        {
            disposing = true;
            foreach (var client in clients)
            {
                if (client.Value.Available)
                {
                    client.Value.Dispose();
                    clients.Remove(client.Key);
                }
            }

        }

        ~Server()
        {
            Dispose();
        }

    }
}
