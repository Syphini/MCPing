using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPing
{
    /// <summary>
    /// C# represenation of the following JSON file
    /// https://gist.github.com/thinkofdeath/6927216
    /// </summary>
    public class PingPayload
    {
        /// <summary>
        /// Protocol that the server is using and the given name
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public VersionPayload Version { get; set; }

        [JsonProperty(PropertyName = "players")]
        public PlayersPayload Players { get; set; }

        [JsonProperty(PropertyName = "description")]
        public JObject Description { get; set; }
        public string Motd { get { return Description.GetValue("text").ToString(); } }

        /// <summary>
        /// Server icon, important to note that it's encoded in base 64
        /// </summary>
        [JsonProperty(PropertyName = "favicon")]
        public string Icon { get; set; }

        public struct VersionPayload
        {
            [JsonProperty(PropertyName = "protocol")]
            public int Protocol { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
        }

        public struct PlayersPayload
        {
            [JsonProperty(PropertyName = "max")]
            public int Max { get; set; }

            [JsonProperty(PropertyName = "online")]
            public int Online { get; set; }

            [JsonProperty(PropertyName = "sample")]
            public List<Player> Sample { get; set; }
        }

        public struct Player
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
        }
    }
    
    public class PingPayloadOld
    {

    }
}
