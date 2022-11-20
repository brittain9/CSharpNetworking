using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace TCPGames
{

    public class TCPGameClient
    {
        public readonly string ServerAddress;
        public readonly int Port;
        public bool Running { get; private set; }
        private TcpClient _client;
        private bool _clientRequestDisconnect = false;

        // Messaging
        private NetworkStream _msgStream = null;
        private Dictionary<string, Func<string, Task>> _commandHandlers = new Dictionary<string, Func<string, Task>>();

        public TCPGameClient(string serverAddress, int port)
        {
            _client = new TcpClient();
            Running = false;

            ServerAddress = serverAddress;
            Port = port;
        }

        private void _cleanupNetworkResources()
        {
            _msgStream?.Close();
            _msgStream = null;
            _client.Close();
        }

        // Connects to game server
        public void Connect()
        {
            // Connect to the server
            try
            {
                _client.Connect(ServerAddress, Port); // resolves DNS for us
            }
            catch (SocketException se)
            {
                Console.WriteLine("[ERROR] {0}", se.Message);
            }

            // Check that we have connected
            if (_client.Connected)
            {
                Console.WriteLine("Connected to the server at {0}.", _client.Client.RemoteEndPoint);
                Running = true;

                _msgStream = _client.GetStream();

                // Hook up some packet command handlers
                _commandHandlers["bye"] = _handleBye;
                _commandHandlers["message"] = _handleMessage;
                _commandHandlers["input"] = _handleInput;
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}:{1}.", ServerAddress, Port);
            }
        }

        // Requests a disconnect, will send a "bye," message to the server
        // This should only be called by the user
        public void Disconnect()
        {
            Console.WriteLine("Disconnecting from the server");
            Running = false;
            _clientRequestDisconnect = true;
            _sendPacket(new Packet("bye")).GetAwaiter().GetResult(); ;
        }

        // Main loop for Games Client
        public void Run()
        {
            bool wasRunning = Running;

            // Listen for messages
            List<Task> tasks = new List<Task>();
            while (Running)
            {
                //Check for new packets
                tasks.Add(_handleIncomingPackets());

                // Use less CPU
                Thread.Sleep(10);

                // Make sure that we didn't disconnect gracefully
                if (_isDisconnected(_client) && !_clientRequestDisconnect)
                {
                    Running = false;
                    Console.WriteLine("The server has disconnected from us ungracefully.\n:[");
                }
            }

            // Just in case there are more packets, give 1 sec to be processed
            Task.WaitAll(tasks.ToArray(), 1000);

            // Cleanup
            _cleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected");
        }

        // Sends packets to the server async
        private async Task _sendPacket(Packet packet)
        {
            try
            {
                // Convert JSON to buffer and its length to a 16 bit unsigned interger buffer
                byte[] jsonBuffer = Encoding.UTF8.GetBytes(packet.ToJson());
                byte[] lengthBuffer = BitConverter.GetBytes(Convert.ToUInt16(jsonBuffer.Length));

                // Join the buffers
                byte[] packetBuffer = new byte[jsonBuffer.Length + lengthBuffer.Length];
                lengthBuffer.CopyTo(packetBuffer, 0);
                jsonBuffer.CopyTo(packetBuffer, packetBuffer.Length);

                // Send the packet
                await _msgStream.WriteAsync(packetBuffer, 0, packetBuffer.Length);
            }
            catch (Exception e)
            {
            }

        }

        // Checks for new incoming messages and handles them
        // This method will handle one Packet at a time, even if more than one is in the memory stream
        private async Task _handleIncomingPackets()
        {
            try
            {
                if (_client.Available > 0)
                {
                    // There must be some incoming data, the first two bytes are the size of the Packet
                    byte[] lengthBuffer = new byte[2];
                    await _msgStream.ReadAsync(lengthBuffer, 0, 2);
                    ushort packetByteSize = BitConverter.ToUInt16(lengthBuffer, 0);

                    // Now read that many bytes from what's left in the stream, it must be the Packet
                    byte[] jsonBuffer = new byte[packetByteSize];
                    await _msgStream.ReadAsync(jsonBuffer, 0, jsonBuffer.Length);

                    // Convert it into a packet datatype
                    string jsonString = Encoding.UTF8.GetString(jsonBuffer);
                    Packet packet = Packet.FromJson(jsonString);

                    // Dispatch it
                    try
                    {
                        await _commandHandlers[packet.Command](packet.Message);
                    }
                    catch (KeyNotFoundException) { }

                    //Console.WriteLine("[RECEIVED]\n{0}", packet);
                }
            }
            catch (Exception) { }
        }

        #region Command Handlers

        private Task _handleBye(string message)
        {
            // Print the message
            Console.WriteLine("The server is disconnecting us with this message:");
            Console.WriteLine(message);

            // Will start the disconnection process in Run()
            Running = false;
            return Task.FromResult(0);  // Task.CompletedTask exists in .NET v4.
        }

        // Just prints out a message sent from the server
        private Task _handleMessage(string message)
        {
            Console.Write(message);
            return Task.FromResult(0);  // Task.CompletedTask exists in .NET v4.6
        }

        // Gets input from the user and sends it to the server
        private async Task _handleInput(string message)
        {
            // Print the prompt and get a response to send
            Console.Write(message);
            string responseMsg = Console.ReadLine();

            // Send the response
            Packet resp = new Packet("input", responseMsg);
            await _sendPacket(resp);
        }
        #endregion

        #region TcpClient Helper Methods
        // Checks if a client has disconnected ungracefully
        // Adapted from: http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
        private static bool _isDisconnected(TcpClient client)
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
        #endregion // TcpClient Helper Methods

        #region Program Execution
        public static TCPGameClient gamesClient;

        public static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            // Perform a graceful disconnect
            args.Cancel = true;
            gamesClient?.Disconnect();
        }

        public static void Main(string[] args)
        {
            // Setup the Games Client
            string host = "localhost";//args[0].Trim();
            int port = 6000;//int.Parse(args[1].Trim());
            gamesClient = new TCPGameClient(host, port);

            // Add a handler for a Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // Try to connecct & interact with the server
            gamesClient.Connect();
            gamesClient.Run();

        }
        #endregion // Program Execution
    }
}