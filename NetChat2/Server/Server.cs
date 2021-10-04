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
        readonly CancellationTokenSource listenerToken = new CancellationTokenSource();
        readonly Dictionary<Guid, ServerClient> clients = new Dictionary<Guid, ServerClient>();

        Guid guid = Guid.NewGuid();

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
                var newClient = new ServerClient(client, guid);
                if (newClient.Available)
                    newClient.OnCommandBroadcastable += BroadcastClientCommand;
                newClient.OnDisconnect += ClientDisconnect;

                // newClient.OnAdvancedCommand += ProcessAdvancedCommand;
                clients.Add(newClient.Guid, newClient);
                BroadcastCommand(new BaseCommand(
                    guid, 
                    CommandTypes.Server, 
                    Commands.ServerText, 
                    new Dictionary<string, string>(){
                            { "TEXT", newClient.UserName + " has connected!\n" }
                        }
                    ));
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                throw;
            }
        }

        private void BroadcastCommand(BaseCommand command)
        {
            if (!Available)
                return;
            foreach (var client in clients.Values)
                client.Write(command);
        }

        private void BroadcastClientCommand(object sender, BaseCommand command)
        {
            BroadcastCommand(command);
        }

        private void ClientDisconnect(object sender)
        {
            var disconnectingClient = ((ServerClient)sender);
            var command = new BaseCommand(
                guid,
                CommandTypes.Server,
                Commands.ServerText,
                new Dictionary<string, string>(){ 
                    { "TEXT", disconnectingClient.UserName + " has disconnected!\n" }
                });
            // It's more user friendly if the user also sees that they have been disconnected.
            BroadcastClientCommand(sender, command);
            clients.Remove(((ServerClient)sender).Guid);
            disconnectingClient.Dispose();
        }

        public new void Dispose()
        {
            disposing = true;
            foreach (var client in clients)
            {
                if (client.Value.Available)
                {
                    client.Value.WriteServerCommand(guid, Commands.ServerClosing);
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
