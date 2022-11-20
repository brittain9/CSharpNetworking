using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPGames
{
    interface IGame
    {
        #region Properties

        // Name of Game
        String Name { get; }

        // How many players needed to start
        int RequiredPlayers { get; }

        #endregion

        #region Functions

        // Adds a player to the game (before it starts)
        bool AddPlayer(TcpClient player);

        void DisconnectClient(TcpClient client);

        // Main game loop
        void Run();

        #endregion
    }
}
