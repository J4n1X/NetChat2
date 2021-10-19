using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

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

        // A reference of this will be passed to all the ServerClients
        public static ConcurrentDictionary<Guid, ServerClient> Clients = new ConcurrentDictionary<Guid, ServerClient>();
        public static bool ServerActive = false;

        public static Guid Guid = Guid.NewGuid();

        #endregion

        public Server()
        {
        }
        public Server(string localIP, int port = 9000) => Listen(localIP, port);

        public void Listen(string localIP, int port = 9000)
        {
            // Prevent server from starting if one is already active or if the server is already running
            if (!available || !ServerActive)
            {
                Clients.Clear();
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
            ServerActive = true;
        }

        private void ProcessNewClient(TcpClient client)
        {
            try
            {
                var newClient = new ServerClient(client, Guid);
                //if (newClient.Available)
                //    newClient.OnCommandBroadcastable += BroadcastClientCommand;
                newClient.OnDisconnect += ClientDisconnect;

                // newClient.OnAdvancedCommand += ProcessAdvancedCommand;
                Clients.TryAdd(newClient.Guid, newClient);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                throw;
            }
        }

        public static void BroadcastServerCommand(BaseCommand command, params ValueTuple<string, string>[] data)
        {
            var args = new Dictionary<string, string>();
            BroadcastCommand(new CommandObject(Server.Guid, CommandTypes.Server, command, data.ToDictionary(x => x.Item1, x => x.Item2)));
        }

        public static void BroadcastCommand(CommandObject command)
        {
            if (!ServerActive)
                return;
            foreach (var client in Server.Clients.Values)
                client.Write(command);
        }

        private void ClientDisconnect(object sender)
        {
            Clients.Remove(((ServerClient)sender).Guid, out _);
        }

        public new void Dispose()
        {
            ServerActive = false;
            disposing = true;
            foreach (var client in Clients)
            {
                if (client.Value.Available)
                {
                    Clients.Remove(client.Key, out ServerClient removedClient);
                    removedClient.WriteServerCommand(Guid, BaseCommand.SERVER_CLOSING);
                    removedClient.Dispose();
                }
            }

        }

        ~Server()
        {
            Dispose();
        }

    }
}
