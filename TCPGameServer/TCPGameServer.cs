using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TCPGames;

namespace TCPGames
{
    public class TCPGameServer
    {
        // Listen for incoming connectgions
        private TcpListener _listener;

        // Client objects
        private List<TcpClient> _clients = new List<TcpClient>();
        private List<TcpClient> _waitingLobby = new List<TcpClient>();

        // Game stuff
        private Dictionary<TcpClient, IGame> _gameClientIsIn = new Dictionary<TcpClient, IGame>();
        private List<IGame> _games = new List<IGame>();
        private List<Thread> _gameThreads = new List<Thread>();
        private IGame _nextGame;

        // Other data
        public readonly string Name;
        public readonly int Port;
        public bool Running { get; private set; }

        // Construct to create new Games server
        public TCPGameServer(string name, int port)
        {
            Name = name;
            Port = port;
            Running = false;

            // Create a listener
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        public void Shutdown()
        {
            if (Running)
            {
                Running = false;
                Console.WriteLine("Shutting down the Game(s) server...");
            }
        }

        public void Run()
        {
            Console.WriteLine("Starting the \"{0}\" Game(s) Server on port {1}.", Name, Port);
            Console.WriteLine("Press Ctrl-C to shutdown the server at any time.");

            // Start the next game
            // (current only the Guess My Number Game)
            _nextGame = new GuessMyNumberGame(this);

            // Start running the server
            _listener.Start();
            Running = true;
            List<Task> newConnectionTask = new List<Task>();
            Console.WriteLine("Waiting for incoming connections");

            while (Running)
            {
                // Handle new clients
                if (_listener.Pending())
                    newConnectionTask.Add(_handleNewConnection());

                // Once we have enough clients for the game, add them and start
                if (_waitingLobby.Count >= _nextGame.RequiredPlayers)
                {
                    // Get that many players from lobby and start
                    int numPlayers = 0;
                    while (numPlayers < _nextGame.RequiredPlayers)
                    {
                        // Pop the first one off
                        TcpClient player = _waitingLobby[0];
                        _waitingLobby.RemoveAt(0);

                        // Try adding it to the game
                        if (_nextGame.AddPlayer(player))
                            numPlayers++;
                        else
                        {
                            _waitingLobby.Add(player);
                        }
                    }

                    // Start the game in a new thread
                    Console.WriteLine("Starting a \"{0}\" game.", _nextGame.Name);
                    Thread gameThread = new Thread(new ThreadStart(_nextGame.Run));
                    gameThread.Start();
                    _games.Add(_nextGame);
                    _gameThreads.Add(gameThread);

                    // Create a new game
                    _nextGame = new GuessMyNumberGame();
                }

                // Check if any clients have disconnected in waiting, gracefully or not
                // NOTE: This could (and should) be parallelized
                foreach (TcpClient client in _waitingLobby.ToArray())
                {
                    EndPoint endPoint = client.Client.RemoteEndPoint;
                    bool disconnected = false;

                    // check for graceful first
                    Packet p = ReceivePacket(client).GetAwaiter().GetResult();
                    disconnected = (p?.Command == "bye");

                    // Then ungraceful
                    disconnected |= IsDisconnected(client);

                    if (disconnected)
                    {
                        HandleDisconnectedClient(client);
                        Console.WriteLine("Client {0} has disconnected from the Game(s) Server.", endPoint);
                    }
                }

                // Take a small nap
                Thread.Sleep(10);
            }

            // In the chance a client connects but we exited the loop, give them 1 sec to finish
            Task.WaitAll(newConnectionTask.ToArray(), 1000);

            // Shutdown all of our threads, regardless if they are done.
            foreach (Thread thread in _gameThreads)
                thread.Abort();

            // Disconnect any clients still here
            Parallel.ForEach(_clients, (client) => { DisconnectClient(client, "The Game(s) Server is being shutdown"); }
            );

            // Cleanup Resources
            _listener.Stop();

            // Info
            Console.WriteLine("The server has been shut down.");
        }

        // Awaits for a new connection and then adds them to the waiting lobby
        private async Task _handleNewConnection()
        {
            // Get the new client using a Future
            TcpClient newClient = await _listener.AcceptTcpClientAsync();
            Console.WriteLine("New connection from {0}.", newClient.Client.RemoteEndPoint);

            // Store them and put them in the waiting lobby
            _clients.Add(newClient);
            _waitingLobby.Add(newClient);

            // Send a welcome message
            string msg = String.Format("Welcome to the \"{0}\" Games Server.\n", Name);
            await SendPacket(newClient, new Packet("message", msg));
        }

        // Will attempt to gracefully disconnect a TcpClient
        // This should be use for clients that may be in a game, or the waiting lobby
        public void DisconnectClient(TcpClient client, string message = "")
        {
            Console.WriteLine("Disconnecting the client from {0}.", client.Client.RemoteEndPoint);

            // If there wasn't a message set, use the default "Goodbye."
            if (message == "")
                message = "Goodbye.";

            // Send the "bye" message
            Task byePacket = SendPacket(client, new Packet("bye", message));

            // Notify a game that might have them
            try
            {
                _gameClientIsIn[client]?.DisconnectClient(client);
            }
            catch (KeyNotFoundException)
            {
            }

            // Give the client some time to send and proccess the graceful disconnect
            Thread.Sleep(100);

            // Cleanup resources on our end
            byePacket.GetAwaiter().GetResult();
            HandleDisconnectedClient(client);
        }

        // Cleans up the resources if a client has disconnected,
        // gracefully or not.  Will remove them from clint list and lobby
        public void HandleDisconnectedClient(TcpClient client)
        {
            // Remove all collections and free resources
            _clients.Remove(client);
            _waitingLobby.Remove(client);
            _cleanupClient(client);
        }

        #region Packet Transmission Methods

        // Sends a packet to the client async
        public async Task SendPacket(TcpClient client, Packet packet)
        {
            try
            {
                // convert JSON to buffer and its length to a 16 bit unsigned integer buffer
                byte[] jsonBuffer = Encoding.UTF8.GetBytes(packet.ToJson());
                byte[] lengthBuffer = BitConverter.GetBytes(Convert.ToUInt16(jsonBuffer.Length));

                // Join the buffers
                byte[] msgBuffer = new byte[lengthBuffer.Length + jsonBuffer.Length];
                lengthBuffer.CopyTo(msgBuffer, 0);
                jsonBuffer.CopyTo(msgBuffer, lengthBuffer.Length);

                // Send the packet
                await client.GetStream().WriteAsync(msgBuffer, 0, msgBuffer.Length);
            }
            catch (Exception e)
            {
                // There was an issue
                Console.WriteLine("There was an issue receiving a packet.");
                Console.WriteLine("Reason: {0}", e.Message);
            }
        }

        // Will get a single packet from a TcpClient
        // Will return null if there isn't any data available or some other
        // issue getting data from the client
        public async Task<Packet> ReceivePacket(TcpClient client)
        {
            Packet packet = null;
            try
            {
                // First check there is data avaliable
                if (client.Available == 0)
                    return null;

                NetworkStream msgStream = client.GetStream();

                // There must be some incoming data, the two bytes are the size of the packet
                byte[] lengthBuffer = new byte[2];
                await msgStream.ReadAsync(lengthBuffer, 0, 2);
                ushort packetByteSize = BitConverter.ToUInt16(lengthBuffer, 0);

                // Now read that many bytes from what's left in the stream, it must be the packet
                byte[] jsonBuffer = new byte[packetByteSize];
                await msgStream.ReadAsync(lengthBuffer, 0, 2);

                // Convert to packet datatype
                string jsonString = Encoding.UTF8.GetString(jsonBuffer);
                packet = Packet.FromJson(jsonString);
            } 
            catch (Exception e)
            {
                // There was an issue in receiving
                Console.WriteLine("There was an issue sending a packet to {0}.", client.Client.RemoteEndPoint);
                Console.WriteLine("Reason: {0}", e.Message);
            }

            return packet;
        }

        #endregion

        #region TCPClient Helper Methods

        // Checks if a client has disconnected ungracefully
        // Adapted from: http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
        public static bool IsDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException)
            {
                // We got a socket error, assume it's disconnected
                return true;
            }
        }

        // cleans up resources for a TcpClient and closes it
        private static void _cleanupClient(TcpClient client)
        {
            client.GetStream().Close();     // Close network stream
            client.Close();                 // Close client
        }

        #endregion

        #region Program Execution

        public static TCPGameServer GamesServer;

        // For when the user Presses ctrl-C, gracefully shutdown server
        public static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            GamesServer?.Shutdown();
        }

        public static void Main(string[] args)
        {
            // Some arguments
            string name = "Bad BBS"; //args[0]
            int port = 6000;

            // Handler for Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // Create and run the server
            GamesServer = new TCPGameServer(name, port);
            GamesServer.Run();
        }
        #endregion
    }
}