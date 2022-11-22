using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UDPFileSender
{
    // This is a Simple data structure that is used in packet queues
    class NetworkMessage
    {
        public IPEndPoint Sender { get; set; }
        public Packet Packet { get; set; }
    }
}
