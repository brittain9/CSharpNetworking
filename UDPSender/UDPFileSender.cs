using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using UdpFileTransfer;

namespace UDPFileSender
{
    class UDPFileSender
    {
        #region Statics

        public static readonly UInt32 MaxBlockSize = 8 * 1024; // 8 KB

        #endregion

        enum SenderState
        {
            NotRunning,
            WaitingForFileRequest,
            SendingFileInfo,
            WaitingForInfoAck,
            Transfering
        }

        // Connection data
        private UdpClient _client;
        public readonly int Port;
        public bool Running { get; private set; } = false;

        // Transfer data
        public readonly string FileDirectory;
        private HashSet<string> _transferableFiles;
        private Dictionary<UInt32, Block> _blocks = new Dictionary<UInt32, Block>();
        private Queue<NetworkMessage> _packetQueue = new Queue<NetworkMessage>();

        // Other stuff
        private MD5 _hasher;

        // Constructor, creates a UdpClient on <port>
        public UDPFileSender(string filesDirectory, int port)
        {
            FileDirectory = filesDirectory;
            Port = port;
            _client = new UdpClient(Port, AddressFamily.InterNetwork);
            _hasher = MD5.Create();
        }

        // Prepares the Sender for the file transfers
        public void Init()
        {
            // Scan files (only top directory)
            List<string> files = new List<string>(Directory.EnumerateFiles(FileDirectory));
            _transferableFiles = new HashSet<string>(files.Select(s => s.Substring(FileDirectory.Length + 1)));

            //  Make sure we have at least one to send
            if (_transferableFiles.Count != 0)
            {
                // Modify the state
                Running = true;

                // Print Info
                Console.WriteLine("I'll transfer these files:");
                foreach (string s in _transferableFiles)
                {
                    Console.WriteLine("  {0}", s);
                }
            }
            else
            {
                Console.WriteLine("No files to transfer.");
            }
        }

        public void Shutdown()
        {
            Running = false;
        }

        public void Run()
        {
            // Tranfer state
            SenderState state = SenderState.WaitingForFileRequest;
            string requestedFile = "";
            IPEndPoint receiver = null;

            // This is a handy little function to reset transfer state
            Action ResetTransferState = new Action(() =>
            {
                state = SenderState.WaitingForFileRequest;
                requestedFile = "";
                receiver = null;
                _blocks.Clear();
            });

            while (Running)
            {
                // Check for messages
                _checkForNetworkMessages();
                NetworkMessage nm = (_packetQueue.Count > 0) ? _packetQueue.Dequeue() : null;

                // Check to see if BYE
                bool isBye = (nm == null) ? false : nm.Packet.IsBye;
                if (isBye)
                {
                    ResetTransferState();
                    Console.WriteLine("Received a BYE message, waiting for next client.");
                }

                // Do the action in current state
                switch (state)
                {
                    case SenderState.WaitingForFileRequest:
                }
            }
        }
    }
}
