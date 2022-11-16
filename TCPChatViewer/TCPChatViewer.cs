using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCPChatViewer
{
    class TCPChatViewer
    {
        // Connection objects
        public readonly string ServerAddr;
        public readonly int Port;
        private TcpClient _client;
        public bool Running { get; private set; }
        private bool _disconnectedRequest = false;

        // Buffer and messaging
        public readonly int BufferSize = 2 * 1024; // 2KB
        private NetworkStream _msgStream = null;

        public TCPChatViewer(string serverAddr, int port)
        {
            // Create a non-connected TcpClient
            _client = new TcpClient();          // Other constructors will start a connection
            _client.SendBufferSize = BufferSize;
            _client.ReceiveBufferSize = BufferSize;
            Running = false;

            // Set the other things
            ServerAddr = serverAddr;
            Port = port;
        }

        public void Connect()
        {
            // Try to connect
            _client.Connect(ServerAddr, Port);
            EndPoint endPoint = _client.Client.RemoteEndPoint;

            // check connection
            if (_client.Connected)
            {
                // got in!
                Console.WriteLine("Connected to the server at {0}.", endPoint);

                // Send them the message that we're a viewer
                _msgStream = _client.GetStream();
                byte[] msgBuffer = Encoding.UTF8.GetBytes("viewer");
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length);
                if (!_isDisconnected(_client))
                {
                    Running = true;
                    Console.WriteLine("Press Ctrl-C to exit the Viewer at any time.");
                }
                else
                {
                    // Server doens't see us as a viewer, cleanup
                    _cleanupNetworkResources();
                    Console.WriteLine("The server didn't recognise us as a Viewer.\n:[");
                }
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
            }
        }

        // Requests a disconnect
        public void Disconnect()
        {
            Running = false;
            _disconnectedRequest = true;
            Console.WriteLine("Disconnecting from the chat...");
        }

        // Main loop, listens and prints messages from the server
        public void ListenForMessages()
        {
            bool wasRunning = Running;

            // Listen for messages
            while (Running)
            {
                // Do we have a new message?
                int messageLength = _client.Available;
                if (messageLength > 0)
                {
                    Console.WriteLine("New incoming message of {0} bytes", messageLength);

                    // Read the whole message
                    byte[] msgBuffer = new byte[messageLength];
                    _msgStream.Read(msgBuffer, 0, messageLength);   // Blocks


                    // Decode it and print it
                    string msg = Encoding.UTF8.GetString(msgBuffer);
                    Console.WriteLine(msg);
                }

                // Use less CPU
                Thread.Sleep(10);

                // Check the server didn't disconnect us
                if (_isDisconnected(_client))
                {
                    Running = false;
                    Console.WriteLine("Server has disconnected from us.\n:[");
                }

                // Check that a cancel has been requested by the user
                Running &= !_disconnectedRequest;
            }

            // Cleanup
            _cleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected.");
        }

        // Cleans any leftover network resources
        private void _cleanupNetworkResources()
        {
            _msgStream?.Close();
            _msgStream = null;
            _client.Close();
        }

        // Checks if a socket has disconnected
        // Adapted from -- http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
        private static bool _isDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException se)
            {
                // We got a socket error, assume it's disconnected
                return true;
            }
        }

        public static TCPChatViewer viewer;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            viewer.Disconnect();
            args.Cancel = true;
        }

        public static void Main(string[] args)
        {
            // Setup the Viewer
            string host = "localhost";//args[0].Trim();
            int port = 6000;//int.Parse(args[1].Trim());
            viewer = new TCPChatViewer(host, port);

            // Add a handler for a Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // Try to connect & view messages
            viewer.Connect();
            viewer.ListenForMessages();
        }
    }
}

