using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPing
{
    using static Functions;
    class ServerPing
    {
        static bool finishedChecking = false;
        static List<string> scannedList;

        static Dictionary<string, ServerListing> initServerDict;
        static ConcurrentDictionary<string, ServerListing> concurrentServerDict;

        const int sleepTime = 150;

        static int currentCount = 0;

        private static void Main(string[] args)
        {
            Console.Title = "Minecraft Server Ping";

            ScanServers();

            //CountRange("1.0.0.0","1.255.255.255");
            //CalculateRange("0.0.0.0", "0.255.255.255");

            //Server.Start();

            Console.ReadKey();
        }

        static void ScanServers()
        {
            #region List Initilization
            //Deserialize all files
            List<string> ipList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.ipListPath));
            scannedList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.scannedPath));
            initServerDict = JsonConvert.DeserializeObject<Dictionary<string, ServerListing>>(File.ReadAllText(Constants.serverListPath));

            //Convert List Types
            concurrentServerDict = new ConcurrentDictionary<string, ServerListing>(initServerDict);
            HashSet<string> hashScanList = new HashSet<string>(scannedList);
            #endregion

            //Add IP's
            AddRange(ipList);

            //Count number of servers to left scan
            int count = 0;
            foreach (var item in ipList)
            {
                if (!hashScanList.Contains(item))
                    count++;
            }

            //Display
            Console.WriteLine($"IP's to Scan: {count}");
            Console.WriteLine($"Servers already registered: {initServerDict.Count}");

            #region Threading
            Thread writeThread = new Thread(new ParameterizedThreadStart(WriteTimer));
            writeThread.Start(ipList.Count);

            foreach (string ip in ipList)
            {
                ServerPing instance = new ServerPing();

                if (!hashScanList.Contains(ip.ToString()) || initServerDict.ContainsKey(ip.ToString()))
                {
                    //ThrowError(ip, find.ip);
                    string tmp = ip;
                    Thread thread = new Thread(() => instance.Ping(tmp))
                    {
                        Name = ip
                    };
                    thread.Start();
                    Thread.Sleep(sleepTime);
                }

            }
            #endregion

            finishedChecking = true;

            Console.ResetColor();
            Console.WriteLine("End of list");
        }

        private void Ping(object ip)
        {
            #region TCP Connection
            var client = new TcpClient();

            if (!IPAddress.TryParse(ip.ToString(), out IPAddress ipaddr))
            {
                ThrowError(ipaddr.ToString(), $"INVALID IP");
            }

            var task = client.ConnectAsync(ipaddr, 25565);

            int attempts = 0;
            while (!task.IsCompleted && attempts < 3)
            {
                Thread.Sleep(250);
                attempts++;
            }

            if (!client.Connected)
            {
                //Test for if in scannedList or serverList
                if (!scannedList.Contains(ipaddr.ToString()) && !initServerDict.ContainsKey(ipaddr.ToString()))
                    scannedList.Add(ipaddr.ToString());
                client.Close();
                return;
            }
            #endregion

            try
            {
                Packet packet = new Packet(client.GetStream(), ipaddr.ToString());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{currentCount} -- Found Server {ip}");
                Console.ResetColor();

                //Grab Server Response
                PingPayload ping = PingStatus(packet);
                client.Close();

                //Initialize a list to hold all users found
                List<string> users = new List<string>();

                //Grab Time
                string currentTime = $"{DateTime.Now.Year:D4}/{DateTime.Now.Month:D2}/{DateTime.Now.Day:D2}, {DateTime.Now.Hour:D2}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2}";


                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"-------------\n{ip}\nTime: {currentTime}\nVersion: {ping.Version.Name}\nPlayers Online: {ping.Players.Online}/{ping.Players.Max}\n-------------");
                //Console.WriteLine(ping.Description);

                //Grab list of predetermined names
                List<string> namesList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.namesPath));

                #region Find Names
                //Scan usernames in server
                if (ping.Players.Sample != null)
                    foreach (var player in ping.Players.Sample)
                    {
                        users.Add(player.Name);

                        //add to users list if corresponding names found
                        if (namesList.Contains(player.Name))
                        {
                            List<string> nameList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.ipListPath));
                            nameList.Add(ip.ToString());
                            string nameSave = JsonConvert.SerializeObject(nameList, Formatting.Indented);
                            Task asyncIP = WriteFileAsync(Constants.ipListPath, nameSave);
                        }
                    }
                #endregion

                //Add information to ServerListing object
                ServerListing info = new ServerListing
                {
                    time = currentTime,
                    ip = ipaddr.ToString(),
                    version = ping.Version.Name,
                    currentPlayers = ping.Players.Online,
                    maxPlayers = ping.Players.Max,
                    playersOnline = users
                };

                //Add or Update current ServerList
                concurrentServerDict.AddOrUpdate(ipaddr.ToString(), info, (key, oldValue) => oldValue = info);

                currentCount++;

                Console.ResetColor();
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
                {
                    ThrowError(ipaddr.ToString(),  "Object was Null", ex);
                }
                else if (ex is IOException)
                {
                    /*
                    * If an IOException is thrown then the server didn't 
                    * send us a VarInt or sent us an invalid one.
                    */
                    ThrowError(ipaddr.ToString(), "Stream forcibly closed", ex);
                }
                else
                {
                    ThrowError(ipaddr.ToString(), "New Error", ex);
                }
            }

        }

        static void WriteTimer(object count)
        {
            const int modif = 100;

            Thread.Sleep(sleepTime * modif);

            for (int i = 0; i < (int)count / modif; i++)
            {
                try
                {
                    //Write to file async
                    string scanOutput = JsonConvert.SerializeObject(scannedList, Formatting.Indented);
                    Task asyncScanList = WriteFileAsync(Constants.scannedPath, scanOutput);
                    Console.WriteLine("Updated Scan");

                    //Write to file async
                    string listOutput = JsonConvert.SerializeObject(concurrentServerDict, Formatting.Indented);
                    Task asyncServer = WriteFileAsync(Constants.serverListPath, listOutput);
                    Console.WriteLine("Writing to Config");
                }
                catch (Exception ex)
                {
                    if (ex is InvalidOperationException)
                        Console.WriteLine($"Error: InvalidOperationException");
                    else if (ex is IOException)
                        Console.WriteLine("Error: IO Exception");
                    else
                        Console.WriteLine($"Error Writing: \n{ex}");

                    continue;
                }

                if (finishedChecking)
                {
                    Console.WriteLine("FINISHED CHECKING");
                    Console.WriteLine($"Server count returned for this scan: {currentCount}");
                    return;
                }

                Thread.Sleep(sleepTime * modif);
            }
        }

        static void AddRange(List<string> _ipList)
        {
            //CURRENT ~

            //HOSTINGER
            _ipList.AddRange(CalculateRange("31.170.160.0", "31.170.163.255"));
            _ipList.AddRange(CalculateRange("31.170.166.0", "31.170.167.255"));
            _ipList.AddRange(CalculateRange("31.220.104.0", "31.220.105.255"));
            _ipList.AddRange(CalculateRange("31.220.107.0", "31.220.109.255"));
            _ipList.AddRange(CalculateRange("31.220.18.0", "31.220.18.255"));
            _ipList.AddRange(CalculateRange("31.220.22.0", "31.220.22.255"));
            _ipList.AddRange(CalculateRange("31.220.48.0", "31.220.63.255"));

            //MCPROHOSTING
            _ipList.AddRange(CalculateRange("104.193.176.0", "104.193.183.255"));
            _ipList.AddRange(CalculateRange("162.244.164.0", "162.244.167.255"));

            //APEX HOSTING
            _ipList.AddRange(CalculateRange("139.99.0.0", "139.99.127.255"));

            //BISECT HOSTING
            _ipList.AddRange(CalculateRange("158.62.200.0", "158.62.207.255"));

            //OVH
            _ipList.AddRange(CalculateRange("135.148.0.0", "135.148.128.255"));
            _ipList.AddRange(CalculateRange("147.135.0.0", "147.135.255.255"));
            _ipList.AddRange(CalculateRange("149.56.0.0", "149.56.255.255"));
            _ipList.AddRange(CalculateRange("51.79.0.0", "51.79.255.255"));
            _ipList.AddRange(CalculateRange("51.81.0.0", "51.81.255.255"));
        }

        public PingPayload PingStatus(Packet packet)
        {
            //Send a "Handshake" packet
            packet.WriteVarInt(754);
            packet.WriteString(packet.ip);
            packet.WriteShort(25565);
            packet.WriteVarInt(1);
            packet.Flush(0);

            //Send a "Status Request" packet
            packet.Flush(0);

            #region Read Data
            byte[] buffer = new byte[short.MaxValue];
            packet.stream.Read(buffer, 0, buffer.Length);

            var length = packet.ReadVarInt(buffer);
            var packetType = packet.ReadVarInt(buffer);

            ThrowError(packet.ip, $"Received packet 0x{packetType:X2} with a length of {length}");

            var jsonLength = packet.ReadVarInt(buffer);
            #endregion

            string json = "";
            try
            {
                json = packet.ReadString(buffer, jsonLength);
                ThrowError(packet.ip, json.Length.ToString());

                if (json != null)
                {
                    return JsonConvert.DeserializeObject<PingPayload>(json);
                }

                ThrowError(packet.ip, "Null Object");
                return ErrorPayload();
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException)
                {
                    ThrowError(packet.ip, "Serialization Error", ex);
                    var old = JsonConvert.DeserializeObject<PingPayload.PingPayloadOld>(json);
                    return new PingPayload()
                    {
                        Players = old.Players,
                        Version = old.Version,
                        Icon = old.Icon,
                        //Description = JObject.Parse((string)old.Description)
                    };
                }
                else
                {
                    ThrowError(packet.ip, "Returning Error Payload", ex);
                }

                return ErrorPayload();
            }
        }

        static async Task WriteFileAsync(string path, string content)
        {
            using (StreamWriter outputFile = new StreamWriter(path))
            {
                await outputFile.WriteAsync(content);
            }
        }

        struct ServerListing
        {
            public string time;
            public string ip;
            public string version;
            public int currentPlayers;
            public int maxPlayers;
            public List<string> playersOnline;
        }

        PingPayload ErrorPayload()
        {
            //CONVERT TO DESCRIPTION/CHAT FORMAT??
            string desc = "{\"text\": \"ERROR\"}";

            return new PingPayload
            {
                Players = new PingPayload.PlayersPayload()
                {
                    Online = 0,
                    Max = 0,
                    Sample = new List<PingPayload.Player>()
                    {
                        new PingPayload.Player()
                        {
                            Id = "ERROR",
                            Name = "ERROR"
                        }
                    }
                },

                Version = new PingPayload.VersionPayload()
                {
                    Name = "ERROR",
                    Protocol = 0
                },

                Icon = "ERROR",

                Description = JObject.Parse(desc)

            };
        }
    }


}
