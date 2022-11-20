using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPGames
{
    class GuessMyNumberGame : IGame
    {
        // game objs
        private TCPGameServer _server;
        private TcpClient _player;
        private Random _rng;
        private bool _needToDisconnectClient = false;

        // Name of game
        public string Name
        {
            get { return "Guess My Number"; }
        }

        public int RequiredPlayers
        {
            get { return 1; }
        }

        // Constructor
        public GuessMyNumberGame(TCPGameServer server)
        {
            _server = server;
            _rng = new Random();
        }

        // Adds only a single player to game
        public bool AddPlayer(TcpClient client)
        {
            // Make sure only 1 player added
            if (_player == null)
            {
                _player = client;
                return true;
            }

            return false;
        }

        // If the client who disconnected is ours, quit game
        public void DisconnectClient(TcpClient client)
        {
            _needToDisconnectClient = (client == _player);
        }

        public void Run()
        {
            // Make sure we have a player
            bool running = (_player != null);
            if (running)
            {
                // Send instruction packet
                Packet introPacket = new Packet("message",
                    "Welcome player, I want you to guess my number.\n" +
                    "It's somewhere between (and including) 1 and 100.\n");
                _server.SendPacket(_player, introPacket).GetAwaiter().GetResult();
            }
            else
            {
                return;
            }

            // Should be [1, 100]
            int theNumber = _rng.Next(1, 101);
            Console.WriteLine("Our number is {0}", theNumber);

            // Some bools for game stats
            bool correct = false;
            bool clientConnected = true;
            bool clientDisconnectedGracefully = false;

            while (running)
            {
                // Poll for input
                Packet inputPacket = new Packet("input", "Your guess: ");
                _server.SendPacket(_player, inputPacket).GetAwaiter().GetResult();

                // Read their answer
                Packet answerPacket = null;
                while (answerPacket == null)
                {
                    answerPacket = _server.ReceivePacket(_player).GetAwaiter().GetResult();
                    Thread.Sleep(10);
                }

                if (answerPacket.Command == "bye");
                {
                    _server.HandleDisconnectedClient(_player);
                    clientDisconnectedGracefully = true;
                }

                // Check input
                if (answerPacket.Command == "input")
                {
                    Packet responsePacket = new Packet("message");

                    int theirGuess;
                    if (int.TryParse(answerPacket.Message, out theirGuess))
                    {
                        // See if they won
                        if (theirGuess == theNumber)
                        {
                            correct = true;
                            responsePacket.Message = "Correct! You win!\n";
                        }
                        else if (theirGuess < theNumber)
                            responsePacket.Message = "Too low.\n";
                        else if (theirGuess > theNumber)
                            responsePacket.Message = "Too high.\n";
                    }
                    else
                    {
                        responsePacket.Message = "That wasn't a valid number, try again.\n";
                    }

                    _server.SendPacket(_player, responsePacket).GetAwaiter().GetResult();
                }
                Thread.Sleep(10);
                running &= !correct;

                // Check for disconnects
                if (!_needToDisconnectClient && !clientDisconnectedGracefully)
                {
                    clientConnected &= !TCPGameServer.IsDisconnected(_player);
                }
                else
                {
                    clientConnected = false;
                }

                running &= clientConnected;
            }

            if (clientConnected)
            {
                _server.DisconnectClient(_player, "Thanks for playing!");
            }
            else
            {
                Console.WriteLine("Client Disconnected from game");
            }

            Console.WriteLine("Ending a {0} game", Name);
        }
    }
}
