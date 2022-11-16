/* TCP (Transmission Control Protocol)
 * When you want to talk to someone over the internet
 * One of the main protocols of Internet Protocol Suite. TCP/IP
 * TCP provides reliable, ordered, and error-checked delivery of a byte stream
 *  In any real world application, you should be using async & multi-threaded code
 * when working with network operations.
 *
 * Program design: Three chunks: Server, Messenger, Viewer (No GUI code)
 * Application layer Protocol: OSI model. Transmit and interpret data
 *
 */

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TCPChatServer
{
    class TCPChatServer
    {
        // What listens in
        private TcpListener _listener;

        // types of clients connected
        private List<TcpClient> _viewers = new List<TcpClient>();
        private List<TcpClient> _messengers = new List<TcpClient>();

        // Names taken by other messengers
        private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();

        // Messages that need to be sent
        private Queue<string> _messageQueue = new Queue<string>();

        // Extra fun data
        public readonly string ChatName;
        public readonly int Port;
        public bool Running { get; set; }

        // Buffer
        public readonly int BufferSize = 2 * 1024; // 2KB

        // Make new TCP chat server, with out provided name
        public TCPChatServer(string chatName, int port)
        {
            // set the basic data
            ChatName = chatName;
            Port = port;
            Running = false;

            // Make the listener listen for connections on any networks
            _listener = new TcpListener(IPAddress.Any, Port);
        }

        public void Shutdown()
        {
            Running = false;
            Console.WriteLine("Shutting down server");
        }

        public void Run()
        {
            // Some info
            Console.WriteLine($"Starting the \"{ChatName}\" TCP Chat Server on port {Port}" +
                              $"Press Ctrl-C to shut down the server at any time.");
            // Make server run
            _listener.Start();
            Running = true;

            // Main loop
            while (Running)
            {
                // Check for new clients
                if (_listener.Pending())
                    _handleNewConnections();
                    

                // Do the rest
                _checkForDisconnects();
                _checkForNewMessages();
                _sendMessages();

                // Use less CPU
                Thread.Sleep(10);
            }
            // Stop the server, clean up clients
            foreach (TcpClient v in _viewers)
            {
                _cleanupClient(v);
            }
            foreach (TcpClient m in _messengers)
                _cleanupClient(m);
            _listener.Stop();

            // Some info
            Console.WriteLine("Server is shut down.");
        }

        private void _handleNewConnections()
        {
            // There is at least one, see what they want
            bool good = false;
            TcpClient newClient = _listener.AcceptTcpClient(); // Blocks
            NetworkStream netStream = newClient.GetStream();

            // Modify the default buffer sizes
            newClient.SendBufferSize = BufferSize;
            newClient.ReceiveBufferSize = BufferSize;

            // Print some info
            EndPoint endPoint = newClient.Client.RemoteEndPoint;
            Console.WriteLine("Handling a client from{0}...", endPoint);

            // Let them identify themselves
            byte[] msgBuffer = new byte[BufferSize];
            int BytesRead = netStream.Read(msgBuffer, 0, msgBuffer.Length);
            Console.WriteLine("Got {0} bytes.", BytesRead);
            if (BytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(msgBuffer, 0 , BytesRead);

                if (msg == "viewer")
                {
                    // They just want to watch
                    good = true;
                    _viewers.Add(newClient);

                    Console.WriteLine($"{endPoint} is a Viewer.");

                    // Send them a hello message
                    msg = String.Format("Welcome to the \"{0}\" Chat Server!", ChatName);
                    msgBuffer = Encoding.UTF8.GetBytes(msg);
                    netStream.Write(msgBuffer,0,BytesRead); // Blocks
                } else if (msg.StartsWith("name"))
                {
                    // might be messanger
                    string name = msg.Substring(msg.IndexOf(':') + 1);

                    if ((name != string.Empty) && (!_names.ContainsValue(name)))
                    {
                        // They're new here, add them in
                        good =true;
                        _names.Add(newClient, name);
                        _messengers.Add(newClient);

                        Console.WriteLine($"{endPoint} is a Messenger with the name {name}");
                        
                        // Tell the viewers we have a messenger
                        _messageQueue.Enqueue(String.Format("{0} has joined the chat.", name));

                    }
                }
                else
                {
                    // Wasn't either a viewer or messenger, clean up anyways
                    Console.WriteLine("Wasn't able to identify {0} as a viewer or messenger.", endPoint);
                    _cleanupClient(newClient);
                }

                // Do we want them?
                if (!good)
                {
                    newClient.Close();
                }
            }
        }

        private void _checkForDisconnects()
        {
            // Check the viewers first
            foreach (var v in _viewers.ToArray())
            {
                if (_isDisconnected(v))
                {
                    Console.WriteLine($"Viewer {v.Client.RemoteEndPoint} has left.");

                    // Clean up on our end
                    _viewers.Remove(v);
                    _cleanupClient(v);
                }
            }

            foreach (var m in _messengers.ToArray())
            {
                if (_isDisconnected(m))
                {
                    // Get info about messenger
                    string name = _names[m];

                    // Tell viewers some has left
                    Console.WriteLine("Messeger {0} has left.", name);
                    _messageQueue.Enqueue(String.Format("{0} has left the chat", name));

                    // clean up on our end
                    _messengers.Remove(m);
                    _names.Remove(m);
                    _cleanupClient(m);
                }
            }
        }

        private void _checkForNewMessages()
        {
            foreach (var m in _messengers.ToArray())
            {
                int mesLen = m.Available;
                if (mesLen > 0)
                {
                    // There is one
                    byte[] msgBuffer = new byte[mesLen];
                    m.GetStream().Read(msgBuffer, 0, msgBuffer.Length);

                    // Attach name to it and shove it into queue
                    string msg = String.Format("{0}: {1}", _names[m], Encoding.UTF8.GetString(msgBuffer));
                    _messageQueue.Enqueue(msg);
                }
            }
        }

        private void _sendMessages()
        {
            foreach (var msg in _messageQueue)
            {
                // Encode message
                byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                // Send the message to each viewer
                foreach (var v in _viewers)
                {
                    v.GetStream().Write(msgBuffer, 0, msgBuffer.Length); // Blocks
                }
            }

            _messageQueue.Clear();
        }

        private static bool _isDisconnected(TcpClient client)
        {
            try
            {
                Socket s = client.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException se)
            {
                // Socket error, assume disconnected
                return true;
            }
        }

        private static void _cleanupClient(TcpClient client)
        {
            client.GetStream().Close(); // Close network stream
            client.Close();
        }

        public static TCPChatServer chat;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            chat.Shutdown();
            args.Cancel = true;
        }

        public static void Main(string[] args)
        {
            // Create the server
            string name = "Alex's Chat Room";
            int port = 6000;
            chat = new TCPChatServer(name, port);

            // Add handler for Ctrl-C
            Console.CancelKeyPress += InterruptHandler;
            // run chat server
            chat.Run();
        }
    }
}



