using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

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
        static List<ServerList> initServerList;

        private static void Main(string[] args)
        {
            Console.Title = "Minecraft Server Ping";

            List<string> ipList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.ipListPath));
            scannedList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.scannedPath));
            initServerList = JsonConvert.DeserializeObject<List<ServerList>>(File.ReadAllText(Constants.serverListPath));

            ipList.AddRange(CalculateRange("139.99.0.0", "139.99.127.255"));
            ipList.AddRange(CalculateRange("158.62.200.0", "158.62.207.255"));
            ipList.AddRange(CalculateRange("162.244.164.0", "162.244.167.255"));
            ipList.AddRange(CalculateRange("147.135.0.0", "147.135.255.255"));
            ipList.AddRange(CalculateRange("104.193.176.0", "104.193.183.255"));
            ipList.AddRange(CalculateRange("149.56.0.0", "149.56.255.255"));
            ipList.AddRange(CalculateRange("51.79.0.0", "51.79.255.255"));
            ipList.AddRange(CalculateRange("135.148.0.0", "135.148.128.255"));

            //~NOT IN USE
            //ipList.AddRange(CalculateRange("192.95.0.0", "192.95.63.255"));
            //ipList.AddRange(CalculateRange("192.99.0.0", "192.99.255.255"));

            HashSet<string> hashScanList = new HashSet<string>(scannedList);

            Console.WriteLine($"IP's to Scan: {ipList.Count}");
            Console.WriteLine($"Servers already registered: {initServerList.Count}");

            Thread writeThread = new Thread(new ParameterizedThreadStart(WriteTimer));
            writeThread.Start(ipList.Count);

            foreach (string ip in ipList)
            {
                ServerPing instance = new ServerPing();
                //instance.Ping(ip);

                //Find an index value corresponding to an IP value in ServerList
                int index = initServerList.FindIndex(f => f.ip == ip.ToString());

                if (!hashScanList.Contains(ip.ToString()) || index >= 0)
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
            const int modif = 100;

            for (int i = 0; i < (int)count / modif; i++)
            {
                Thread.Sleep(150 * modif);
                if (!finishedChecking)
                {
                    try
                    {
                        string output = JsonConvert.SerializeObject(scannedList, Formatting.Indented);
                        File.WriteAllText(Constants.scannedPath, output);
                        Console.WriteLine("Saved");
                    }
                    catch (Exception ex)
                    {
                        if (ex is InvalidOperationException)
                            Console.WriteLine($"Error: InvalidOperationException");
                        else if (ex is IOException)
                            Console.WriteLine("Error: IO Exception");
                        else
                            Console.WriteLine(ex);
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

                if (start[2] == end[2] && start[3] == end[3])
                    return list;

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

                if (!scannedList.Contains(ipaddr.ToString()))
                    scannedList.Add(ipaddr.ToString());
                return;
            }

            try
            {
                Packet packet = new Packet(client.GetStream(), new List<byte>());

                PingPayload ping = packet.PingStatus(packet);

                Console.ForegroundColor = ConsoleColor.Green;

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
                    List<ServerList> serverList = JsonConvert.DeserializeObject<List<ServerList>>(File.ReadAllText(Constants.serverListPath));

                    //Grab list of predetermined names
                    List<string> namesList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.namesPath));

                    //Scan usernames in server
                    if (ping.Players.Sample != null)
                        foreach (var player in ping.Players.Sample)
                        //add to users list if corresponding names found
                        {
                            users.Add(player.Name);
                            //if (namesList.Contains(player.Name))
                               // users.Add(player.Name);
                        }

                    //Add information to serverList object
                    ServerList current = new ServerList
                    {
                        time = $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}, {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second:D2}",
                        ip = ipaddr.ToString(),
                        version = ping.Version.Name,
                        currentPlayers = ping.Players.Online,
                        maxPlayers = ping.Players.Max,
                        playersOnline = users
                    };

                    int index = serverList.FindIndex(f => f.ip == ipaddr.ToString());
                    if (index < 0)
                        serverList.Add(current);
                    else
                    {
                        serverList[index] = current;
                        Console.WriteLine("Known Server");
                    }
                        
                    //Write to file
                    string serialized = JsonConvert.SerializeObject(serverList, Formatting.Indented);
                    File.WriteAllText(Constants.serverListPath, serialized);
                    Console.WriteLine("Writing to Config");

                    Console.ResetColor();

                }
                else
                {
                    //Console.ForegroundColor = ConsoleColor.Yellow;
                    //Console.WriteLine($"Incorrect Server Format {ip}");
                    //Console.ResetColor();
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

        struct ServerList
        {
            public string time;
            public string ip;
            public string version;
            public int currentPlayers;
            public int maxPlayers;
            public List<string> playersOnline;
        }
    }
}
