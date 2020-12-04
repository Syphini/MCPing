using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPing
{
    class ServerPing
    {
        private static readonly Dictionary<char, ConsoleColor> Colours = new Dictionary<char, ConsoleColor>
        {
             { '0', ConsoleColor.Black       },
             { '1', ConsoleColor.DarkBlue    },
             { '2', ConsoleColor.DarkGreen   },
             { '3', ConsoleColor.DarkCyan    },
             { '4', ConsoleColor.DarkRed     },
             { '5', ConsoleColor.DarkMagenta },
             { '6', ConsoleColor.Yellow      },
             { '7', ConsoleColor.Gray        },
             { '8', ConsoleColor.DarkGray    },
             { '9', ConsoleColor.Blue        },
             { 'a', ConsoleColor.Green       },
             { 'b', ConsoleColor.Cyan        },
             { 'c', ConsoleColor.Red         },
             { 'd', ConsoleColor.Magenta     },
             { 'e', ConsoleColor.Yellow      },
             { 'f', ConsoleColor.White       },
             { 'k', Console.ForegroundColor  },
             { 'l', Console.ForegroundColor  },
             { 'm', Console.ForegroundColor  },
             { 'n', Console.ForegroundColor  },
             { 'o', Console.ForegroundColor  },
             { 'r', ConsoleColor.White       }
        };

        static bool finishedChecking = false;
        static List<string> scannedList;

        private static void Main(string[] args)
        {
            Console.Title = "Minecraft Server Ping";

            List<string> ipList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("config/iplist.json"));
            scannedList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("config/scanned.json"));

            ipList.AddRange(CalculateRange("51.79.0.0", "51.79.255.255"));
            ipList.AddRange(CalculateRange("104.193.176.0", "104.193.183.255"));
            ipList.AddRange(CalculateRange("149.56.0.0", "149.56.255.255"));


            Console.WriteLine($"IP Count: {ipList.Count}");

            Thread writeThread = new Thread(new ParameterizedThreadStart(WriteTimer));
            writeThread.Start(ipList.Count);

            foreach (string ip in ipList)
            {
                ServerPing instance = new ServerPing();
                //instance.Ping(ip);

                if (!scannedList.Contains(ip.ToString()))
                {
                    Thread thread = new Thread(new ParameterizedThreadStart(instance.Ping));
                    thread.Start(ip);
                    thread.Name = ip;
                    Thread.Sleep(150);
                }

            }

            finishedChecking = true;

            Console.WriteLine("End of list");
            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("\nEnd of IP List\nQuitting...");
            Console.ReadKey();
        }

        static void WriteTimer(object count)
        {
            const int modif = 50;

            for (int i = 0; i < (int)count / modif; i++)
            {
                Thread.Sleep(150 * modif);
                if (!finishedChecking)
                {
                    try
                    {
                        string output = JsonConvert.SerializeObject(scannedList, Formatting.Indented);
                        File.WriteAllText("config/scanned.json", output);
                        Console.WriteLine("Saved");
                    }
                    catch (Exception ex)
                    {
                        if (ex is InvalidOperationException || ex is IOException)
                            Console.WriteLine($"Error during serialization: {ex}");
                    }
                }
                else
                {
                    Console.WriteLine("FINISHED CHECKING");
                    return;
                }
            }
        }

        static int[] ConvertIP(string ip)
        {
            int[] array = new int[4];
            for (int i = 0; i < 3; i++)
            {
                int index = ip.LastIndexOf('.') + 1;
                array[i] = int.Parse(ip.Substring(index, ip.Length - index));
                ip = ip.Remove(index - 1, ip.Length - (index - 1));
            }

            array[3] = int.Parse(ip);
            Array.Reverse(array);

            return array;

        }

        static List<string> CalculateRange(string startIP, string endIP)
        {
            List<string> list = new List<string>();

            //Console.WriteLine($"StartIP: {startIP}, Index: {startIP.LastIndexOf('.')}");
            int[] start = ConvertIP(startIP);
            int[] end = ConvertIP(endIP);

            start[3] -= 1;

            do
            {
                start[3]++;
                if (start[3] == 256)
                {
                    start[2]++;
                    start[3] = 0;
                }
                else if (start[2] != 256)
                {
                    list.Add($"{start[0]}.{start[1]}.{start[2]}.{start[3]}");
                }

            }
            while (start[2] < 256 && start[2] < (end[2] + 1));

            return list;
        }

        private void Ping(object ip)
        {
            var client = new TcpClient();

            //TRY NOT TO USE DNS, IT CURRENTLY DOES NOT WORK
            if (!IPAddress.TryParse(ip.ToString(), out IPAddress ipaddr))
                ipaddr = Dns.GetHostEntry(ip.ToString()).AddressList[0];

            var task = client.ConnectAsync(ipaddr, 25565);
            //Console.WriteLine("Connecting to Minecraft server..");

            int attempts = 0;
            while (!task.IsCompleted && attempts < 3)
            {
                //Console.WriteLine("Connecting..");
                Thread.Sleep(250);
                attempts++;
            }

            if (!client.Connected)
            {
                //Console.ForegroundColor = ConsoleColor.Red;
                //Console.WriteLine($"Unable to connect to {ip}");
                //Console.ResetColor();

                scannedList.Add(ipaddr.ToString());
                return;
            }

            try
            {
                Packet packet = new Packet(client.GetStream(), new List<byte>());

                //Console.WriteLine("Sending status request");

                //Send a "Handshake" packet
                packet.WriteVarInt(47);
                packet.WriteString("localhost");
                packet.WriteShort(25565);
                packet.WriteVarInt(1);
                packet.Flush(0);

                //Send a "Status Request" packet
                packet.Flush(0);


                byte[] buffer = new byte[short.MaxValue];
                packet.stream.Read(buffer, 0, buffer.Length);


                var length = packet.ReadVarInt(buffer);
                var packetType = packet.ReadVarInt(buffer);
                var jsonLength = packet.ReadVarInt(buffer);

                //Console.WriteLine("Received packet 0x{0} with a length of {1}", packetType.ToString("X2"), length);

                var json = packet.ReadString(buffer, jsonLength);
                var ping = JsonConvert.DeserializeObject<PingPayload>(json);

                Console.ForegroundColor = ConsoleColor.Green;
                //Console.WriteLine("Version: {0}", ping.Version.Name);
                //Console.WriteLine("Protocol: {0}", ping.Version.Protocol);
                //Console.WriteLine("Players Online: {0}/{1}", ping.Players.Online, ping.Players.Max);

                //Initialize a list to hold all users found
                List<string> users = new List<string>();

                //Check for specific server settings
                if (true)//ping.Players.Max == 20 && ping.Version.Name == "1.12.2") //Add on MOTD checks??? ~ Probs null and "" checks
                {
                    Console.WriteLine($"Found Server {ip}");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Version: {ping.Version.Name}");
                    Console.WriteLine("Players Online: {0}/{1}", ping.Players.Online, ping.Players.Max);

                    //Grab the current serverList
                    ServerList serverList = JsonConvert.DeserializeObject<ServerList>(File.ReadAllText("config/serverList.json"));

                    //Grab list of predetermined names
                    List<string> namesList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("config/names.json"));

                    //Scan usernames in server
                    if (ping.Players.Sample != null)
                        foreach (Player player in ping.Players.Sample)
                        //add to users list if corresponding names found
                        {
                            users.Add(player.Name);
                            //if (namesList.Contains(player.Name))
                               // users.Add(player.Name);
                        }
                            

                    //Add IP and users to serverList object
                    Dictionary<string, List<string>> dict = serverList.ping;
                    if (!dict.ContainsKey(ipaddr.ToString()))
                    {
                        dict.Add(ipaddr.ToString(), users);
                    }
                    else
                    {
                        dict[ipaddr.ToString()] = users;
                        Console.WriteLine("Known Server");
                    }

                    //Write to file
                    serverList.ping = dict;
                    string serialized = JsonConvert.SerializeObject(serverList, Formatting.Indented);
                    File.WriteAllText("config/serverList.json", serialized);
                    Console.WriteLine("Writing to Config");

                    Console.ResetColor();

                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Incorrect Server Format {ip}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                if (ex is NullReferenceException)
                {
                    Console.WriteLine($"Null stream: \n{ex}");
                }
                else if (ex is IOException)
                {
                    Console.WriteLine($"Stream forcibly closed: \n{ex}");
                }
                else
                {
                    /*
                    * If an IOException is thrown then the server didn't 
                    * send us a VarInt or sent us an invalid one.
                    */

                    Console.WriteLine("Unable to read packet length from server,");
                    Console.WriteLine("are you sure it's a Minecraft server?");
                    Console.WriteLine("Here are the details:");
                    Console.WriteLine(ex);
                }

                Console.ResetColor();
            }

        }

        private void WriteMotd(PingPayload ping)
        {
            Console.Write("Motd: ");
            var chars = ping.Motd.ToCharArray();
            for (var i = 0; i < ping.Motd.Length; i++)
            {
                try
                {
                    if (chars[i] == '\u00A7' && Colours.ContainsKey(chars[i + 1]))
                    {
                        Console.ForegroundColor = Colours[chars[i + 1]];
                        continue;
                    }
                    if (chars[i - 1] == '\u00A7' && Colours.ContainsKey(chars[i]))
                    {
                        continue;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // End of string
                }
                Console.Write(chars[i]);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        
    }

    #region Server ping 
    /// <summary>
    /// C# represenation of the following JSON file
    /// https://gist.github.com/thinkofdeath/6927216
    /// </summary>
    class PingPayload
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
    }

    class VersionPayload
    {
        [JsonProperty(PropertyName = "protocol")]
        public int Protocol { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }

    class PlayersPayload
    {
        [JsonProperty(PropertyName = "max")]
        public int Max { get; set; }

        [JsonProperty(PropertyName = "online")]
        public int Online { get; set; }

        [JsonProperty(PropertyName = "sample")]
        public List<Player> Sample { get; set; }
    }

    class Player
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
    }

    class ServerList
    {
        public Dictionary<string, List<string>> ping;
    }
    #endregion
}
