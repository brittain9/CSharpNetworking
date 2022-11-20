using System.Net.Sockets;
using Newtonsoft.Json;

/* Using JSON, which will only contain plaintext: JSON obj includes fields: command & message
 * command: this tells the client/server how the packet should be processed
 * message: this adds some extra context for the command.  It may be an empty string sometimes
 * App will operate:
 *
 * Client can send bye to server in which case it cleans up resources
 * Server sends bye to client and prints disconnect msg
 *
 * message can be sent from server to client
 *
 * server will send input prompt
 * user input sent from client to server
 */
namespace TCPGames
{
    public class Packet
    {
        /* Before we shove the packet into the internet tubes out there we need to preprocess
         * Call ToJson() method on packet to get as string. Then encode to UTF-8 and send to byte array
         *
         */

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("message")] 
        public string Message { get; set; }

        // Makes a packet
        public Packet(string command = "", string message = "")
        {
            Command = command;
            Message = message;
        }

        public override string ToString()
        {
            return string.Format(
                "[Packet:\n" +
                "  Command=`{0}`\n" +
                "  Message=`{1}`]",
                Command, Message);
        }

        // Serialize to Json
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        // Deserialize
        public static Packet FromJson(String jsonData)
        {
            return JsonConvert.DeserializeObject<Packet>(jsonData);
        }
    }

    public class TCPGameServer
    {
        /*
         *
         *
         */

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
    }
}

