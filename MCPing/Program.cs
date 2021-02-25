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
        static bool finalStop = false;

        static bool finishedChecking = false;
        
        static List<string> initScannedList;
        static ConcurrentBag<string> scannedBag;

        static Dictionary<string, ServerListing> initServerDict;
        static ConcurrentDictionary<string, ServerListing> concurrentServerDict;

        static List<RangeStruct> rangeList;

        const int sleepTime = 150;
        const int modif = 100;

        static int currentCount = 0;
        static int loopCount = 0;

        private static void Main(string[] args)
        {
            Console.Title = "Minecraft Server Ping";
            ASCIITitle();

            ScanServers();

            //CountRange("1.0.0.0","1.255.255.255");
            //CalculateRange("0.0.0.0", "0.255.255.255");

            //PacketTests();

            Console.ReadKey();
        }

        static void ASCIITitle()
        {
            string[] data = File.ReadAllLines(Constants.ASCIIPath);
            Console.WriteLine();

            for (int i = 0; i < data.Length; i++)
            {
                Thread.Sleep(50);
                Console.WriteLine(data[i]);
            }

            Console.WriteLine("\n\n");
        }

        static void PacketTests()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("1 for Server\n2 for Client\nAll else will quit.\n");
            ConsoleKeyInfo key = Console.ReadKey();
            Console.WriteLine($"\nKEY: {key.Key}");

            if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nSERVER START");
                Console.ResetColor();

                Server.Start();
            }
            else if (key.Key == ConsoleKey.D2 || key.Key == ConsoleKey.NumPad2)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nCLIENT START");
                Console.ResetColor();

                //Client.Start();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        static void ScanServers()
        {
            while (!finalStop)
            {
                //Loop start
                finishedChecking = false;

                #region List Initilization
                //Deserialize all files
                rangeList = JsonConvert.DeserializeObject<List<RangeStruct>>(File.ReadAllText(Constants.ipListPath));
                initScannedList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.scannedPath));
                initServerDict = JsonConvert.DeserializeObject<Dictionary<string, ServerListing>>(File.ReadAllText(Constants.serverListPath));

                //Convert List Types
                concurrentServerDict = new ConcurrentDictionary<string, ServerListing>(initServerDict);
                scannedBag = new ConcurrentBag<string>(initScannedList);
                HashSet<string> hashScanList = new HashSet<string>(initScannedList);
                #endregion

                //Add IP's
                List<string> ipList = AddRange(rangeList);

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
                foreach (string ip in ipList)
                {
                    ServerPing instance = new ServerPing();

                    if (!hashScanList.Contains(ip.ToString()) || initServerDict.ContainsKey(ip.ToString()))
                    {
                        if (ip == Constants.saveKey)
                        {
                            finishedChecking = false;
                            Thread writeThread = new Thread(WriteFiles);
                            writeThread.Start();
                            hashScanList = new HashSet<string>(scannedBag);
                        }
                        else
                        {
                            string tmp = ip;
                            Thread thread = new Thread(() => instance.Ping(tmp))
                            {
                                Name = ip
                            };
                            thread.Start();
                        }

                        Thread.Sleep(sleepTime);
                    }

                }
                #endregion

                Console.ResetColor();

                Console.WriteLine("FINISHED CHECKING");
                Console.WriteLine($"Server count returned for this scan: {currentCount}");
                Console.WriteLine("End of list");

                //20 sec sleep
                Thread.Sleep(sleepTime * modif + 5000);

                //Update Debug Counts
                currentCount = 0;
                loopCount++;
            }
        }

        private void Ping(object ip)
        {
            #region TCP Connection
            var client = new TcpClient();

            if (!IPAddress.TryParse(ip.ToString(), out IPAddress ipaddr))
            {
                ThrowError(ipaddr, $"INVALID IP");
            }

            //Later add in 25575 because Shockbyte servers don't use default ports without extra payment
            Task task = client.ConnectAsync(ipaddr, 25565);

            int attempts = 0;
            while (!task.IsCompleted && attempts < 3)
            {
                Thread.Sleep(250);
                attempts++;
            }

            if (!client.Connected)
            {
                //Test for if in scannedList or serverList
                if (!initScannedList.Contains(ipaddr.ToString()) && !initServerDict.ContainsKey(ipaddr.ToString()))
                    scannedBag.Add(ipaddr.ToString());
                client.Close();
                return;
            }
            #endregion

            try
            {
                Packet packet = new Packet(client.GetStream(), ipaddr);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"L{loopCount}:C{currentCount} -- Found Server {ip}");
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

                        //add to users list if corresponding name is found
                        if (namesList.Contains(player.Name))
                        {
                            RangeStruct addName = new RangeStruct
                            {
                                info = $"{currentTime} @ {player.Name}:{player.Id}",
                                startip = ip.ToString(),
                                endip = ip.ToString()
                            };

                            var nameIndex = rangeList.FindIndex(e => e.info.Contains(player.Name));
                            var ipIndex = rangeList.FindIndex(e => e.info.Contains(player.Name) && e.startip == ip.ToString());
                            
                            if (nameIndex >= 0)
                            {
                                //Intention is to modify a way for program to find when a users ip has "changed"
                                if (ipIndex >= 0)
                                {
                                    rangeList[ipIndex] = addName;
                                }
                                else
                                {
                                    rangeList.Add(addName);
                                }
                            }
                            else
                            {
                                rangeList.Add(addName);
                            }
                            

                            string nameSave = JsonConvert.SerializeObject(rangeList, Formatting.Indented);
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
                    ThrowError(ipaddr,  "Object was Null", ex);
                }
                else if (ex is IOException)
                {
                    /*
                    * If an IOException is thrown then the server didn't 
                    * send us a VarInt or sent us an invalid one.
                    */
                    ThrowError(ipaddr, "Stream forcibly closed", ex);
                }
                else
                {
                    ThrowError(ipaddr, "New Error", ex);
                }
            }

        }

        static void WriteFiles()
        {
            while (!finishedChecking)
            {
                //CURRENTLY THESE FUNCTIONS TAKE UP A TREMENOUDOUS AMOUNT OF CPU POWER PROPORTIONATE TO THE SIZE OF THEIR FILES
                //Consider Appending?
                try
                {
                    //Write scanned ip's to file async
                    string scanOutput = JsonConvert.SerializeObject(scannedBag, Formatting.Indented);
                    Task asyncScanList = WriteFileAsync(Constants.scannedPath, scanOutput);

                    //Output time directly
                    Console.WriteLine($"{DateTime.Now.Year:D4}/{DateTime.Now.Month:D2}/{DateTime.Now.Day:D2}, {DateTime.Now.Hour:D2}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2} ---- Updated Scan");

                    //Write detected servers to file async
                    string listOutput = JsonConvert.SerializeObject(concurrentServerDict, Formatting.Indented);
                    Task asyncServer = WriteFileAsync(Constants.serverListPath, listOutput);

                    //Output time directly
                    Console.WriteLine($"{DateTime.Now.Year:D4}/{DateTime.Now.Month:D2}/{DateTime.Now.Day:D2}, {DateTime.Now.Hour:D2}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2} ---- Writing to Config");

                    finishedChecking = true;
                }
                catch (Exception ex)
                {
                    string currentTime = $"{ DateTime.Now.Year:D4}/{ DateTime.Now.Month:D2}/{ DateTime.Now.Day:D2}, { DateTime.Now.Hour:D2}:{ DateTime.Now.Minute:D2}:{ DateTime.Now.Second:D2}";

                    if (ex is InvalidOperationException)
                        Console.WriteLine($"{currentTime} ---- Error: InvalidOperationException");
                    else if (ex is IOException)
                        Console.WriteLine($"{currentTime} ---- Error: IO Exception");
                    else
                        Console.WriteLine($"{currentTime} ---- Error Writing: \n{ex}");

                    //Move to start of next loop without waiting
                    continue;
                }
            }
        }

        static List<string> AddRange(List<RangeStruct> _rangeList)
        {
            List<string> _ipList = new List<string>();

            foreach (var item in _rangeList)
            {
                _ipList.AddRange(CalculateRange(item.startip, item.endip));
                _ipList.Add(Constants.saveKey);
            }

            return _ipList;
        }

        public PingPayload PingStatus(Packet packet)
        {
            //Send a "Handshake" packet
            packet.WriteVarInt(754);
            packet.Write("localhost");
            packet.Write((short)25565);
            packet.WriteVarInt(1);
            packet.MCFlush(0);

            //Send a "Status Request" packet
            packet.MCFlush(0);

            //Read Data
            byte[] buffer = packet.ReadStream();
            var jsonLength = packet.ReadVarInt(buffer);

            string json = "";
            try
            {
                json = packet.ReadString(buffer, jsonLength);
                int jsonCount = json.TrimEnd('\0').Length;

                ThrowError(packet.ip, $"{jsonCount}/{json.Length} Valid Characters");

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

        struct RangeStruct
        {
            public string info;
            public string startip;
            public string endip;
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
