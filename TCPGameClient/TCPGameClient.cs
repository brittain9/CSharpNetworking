using Newtonsoft.Json;

namespace TCPGames
{
    public class Packet
    {
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

    public class TCPGameClient
    {

    }
}