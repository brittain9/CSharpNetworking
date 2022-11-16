using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCPChatMessenger
{
    class TCPChatMessenger
    {
        // Connection objects
        public readonly string ServerAddr;
        public readonly int Port;
        private TcpClient _client { get; set; }
        public bool Running { get; private set; }

        // Buffer & messaging
        public readonly int BufferSize = 2 * 1024; // 2KB
        private NetworkStream _msgStream = null;

        // Personal Data
        public readonly string Name;

        public TCPChatMessenger(String serverAddr, int port, string name)
        {
            // Create a non-connected TcpClient
            _client = new TcpClient(); // Other constructors will start connection
            _client.SendBufferSize = BufferSize;
            _client.ReceiveBufferSize = BufferSize;
            Running = false;

            // Set other things
            ServerAddr = serverAddr;
            Port = port;
            Name = name;
        }

        public void Connect()
        {
            // Try to connect
            _client.Connect(ServerAddr, Port); // Will resolve DNS for us
            EndPoint endPoint = _client.Client.RemoteEndPoint;

            // MAke sure we are connected
            if (_client.Connected)
            {
                // Got in!
                Console.WriteLine("Connected to the server at {0}", endPoint);

                // Tell them that we're a messenger
                _msgStream = _client.GetStream();
                byte[] msgBuffer = Encoding.UTF8.GetBytes(String.Format("name:{0}", Name));
                _msgStream.Write(msgBuffer, 0, msgBuffer.Length);

                // If we're still connected after sending our name, that accepts us
                if (!_isDisconnected(_client))
                {
                    Running = true;
                }
                else
                {
                    // Name was probably taken
                    _cleanupNetworkResources();
                    Console.WriteLine("The server rejected us; \"{0}\" is probably is use.", Name);

                }
            }
            else
            {
                _cleanupNetworkResources();
                Console.WriteLine("Wasn't able to connect to the server at {0}.", endPoint);
            }
        }

        public void SendMessages()
        {
            bool wasRunning = Running;

            while (Running)
            {
                // Poll user for input
                Console.Write("{0}> ", Name);
                string msg = Console.ReadLine();

                // Quit or send message
                if(msg.ToLower() == "quit" || msg.ToLower() == "exit")
                {
                    // User wants to quit
                    Console.WriteLine("Disconnecting...");
                    Running = false;
                }
                else if(msg != String.Empty)
                {
                    // Send the message
                    byte[] msgBuffer = Encoding.ASCII.GetBytes(msg);
                    _msgStream.Write(msgBuffer,0, msgBuffer.Length);
                }

                // Use less CPU
                Thread.Sleep(10);

                // Check server didn't disconnect us
                if (_isDisconnected(_client))
                {
                    // User wants to quit
                    Console.WriteLine("Disconnecting...");
                    Running = false;
                }
            }
            _cleanupNetworkResources();
            if (wasRunning)
            {
                Console.WriteLine("Disconnected");
            }
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

        public static void Main(string[] args)
        {
            // Get a name
            Console.WriteLine("Enter a name to use:");
            string name = Console.ReadLine();

            // Setup messenger
            string host = "localhost";
            int Port = 6000;
            TCPChatMessenger messenger = new TCPChatMessenger(host, Port, name);

            // Connect and send messages
            messenger.Connect();
            messenger.SendMessages();
        }
    }
}