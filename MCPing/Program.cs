using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MCPing
{
    class ServerPing
    {
        static bool finishedChecking = false;
        static List<string> scannedList;

        //static List<ServerList> initServerList;
        //static List<ServerList> currentServerList;

        static Dictionary<string, ServerList> initServerDict;
        static ConcurrentDictionary<string, ServerList> concurrentServerDict;

        const int sleepTime = 150;

        static int currentCount = 0;

        private static void Main(string[] args)
        {
            Console.Title = "Minecraft Server Ping";

            //Annoying me
            args = null;

            //Simplify ~~
            List<string> ipList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.ipListPath));
            scannedList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.scannedPath));
            
            //initServerList = JsonConvert.DeserializeObject<List<ServerList>>(File.ReadAllText(Constants.serverListPath));
            //currentServerList = initServerList;

            initServerDict = JsonConvert.DeserializeObject<Dictionary<string, ServerList>>(File.ReadAllText(Constants.concurrentDictPath));
            concurrentServerDict = new ConcurrentDictionary<string, ServerList>(initServerDict);

            //CURRENT ~

            //HOSTINGER
            ipList.AddRange(CalculateRange("31.170.160.0", "31.170.163.255"));
            ipList.AddRange(CalculateRange("31.170.166.0", "31.170.167.255"));
            ipList.AddRange(CalculateRange("31.220.104.0", "31.220.105.255"));
            ipList.AddRange(CalculateRange("31.220.107.0", "31.220.109.255"));
            ipList.AddRange(CalculateRange("31.220.18.0", "31.220.18.255"));
            ipList.AddRange(CalculateRange("31.220.22.0", "31.220.22.255"));
            ipList.AddRange(CalculateRange("31.220.48.0", "31.220.63.255"));

            //MCPROHOSTING
            ipList.AddRange(CalculateRange("104.193.176.0", "104.193.183.255"));
            ipList.AddRange(CalculateRange("162.244.164.0", "162.244.167.255"));

            //APEX HOSTING
            ipList.AddRange(CalculateRange("139.99.0.0", "139.99.127.255"));

            //BISECT HOSTING
            ipList.AddRange(CalculateRange("158.62.200.0", "158.62.207.255"));

            //OVH
            ipList.AddRange(CalculateRange("135.148.0.0", "135.148.128.255"));
            ipList.AddRange(CalculateRange("147.135.0.0", "147.135.255.255"));
            ipList.AddRange(CalculateRange("149.56.0.0", "149.56.255.255"));
            ipList.AddRange(CalculateRange("51.79.0.0", "51.79.255.255"));
            ipList.AddRange(CalculateRange("51.81.0.0", "51.81.255.255"));

            HashSet<string> hashScanList = new HashSet<string>(scannedList);

            int count = 0;
            foreach (var item in ipList)
            {
                if (!hashScanList.Contains(item))
                    count++;
            }

            Console.WriteLine($"IP's to Scan: {count}");
            Console.WriteLine($"Servers already registered: {initServerDict.Count}");

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

            finishedChecking = true;

            Console.ResetColor();
            Console.WriteLine("End of list");
            //Console.ForegroundColor = ConsoleColor.Yellow;
            //Console.WriteLine("\nEnd of IP List\nQuitting...");
            Console.ReadKey();
        }

        static void WriteTimer(object count)
        {
            const int modif = 100;

            Thread.Sleep(sleepTime * modif);

            for (int i = 0; i < (int)count / modif; i++)
            {
                try
                {
                    //Write to file
                    string scanOutput = JsonConvert.SerializeObject(scannedList, Formatting.Indented);
                    File.WriteAllText(Constants.scannedPath, scanOutput);
                    Console.WriteLine("Saved");

                    string listOutput = JsonConvert.SerializeObject(concurrentServerDict, Formatting.Indented);
                    Task asyncTask = WriteFileAsync(Constants.concurrentDictPath, listOutput);
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

            try
            {
                Packet packet = new Packet(client.GetStream(), new List<byte>(), ipaddr.ToString());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{currentCount} -- Found Server {ip}");
                Console.ResetColor();

                PingPayload ping = packet.PingStatus(packet);
                client.Close();

                //Initialize a list to hold all users found
                List<string> users = new List<string>();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"-------------\n{ip}\nVersion: {ping.Version.Name}\nPlayers Online: {ping.Players.Online}/{ping.Players.Max}\n-------------");
                //Console.WriteLine(ping.Description);

                //Grab list of predetermined names
                List<string> namesList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.namesPath));

                //Scan usernames in server
                if (ping.Players.Sample != null)
                    foreach (var player in ping.Players.Sample)
                    {
                        users.Add(player.Name);
                        try
                        {
                            //add to users list if corresponding names found
                            if (namesList.Contains(player.Name))
                            {
                                List<string> nameList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Constants.ipListPath));
                                nameList.Add(ip.ToString());
                                string nameSave = JsonConvert.SerializeObject(nameList, Formatting.Indented);
                                File.WriteAllText(Constants.ipListPath, nameSave);
                            }
                        }
                        catch (Exception ex)
                        {
                            ThrowError(ip.ToString(), $"FOUND NAMES BUT NOT SAVED", ex);
                        }
                    }

                //Add information to serverList object
                ServerList info = new ServerList
                {
                    time = $"{DateTime.Now.Year:D4}/{DateTime.Now.Month:D2}/{DateTime.Now.Day:D2}, {DateTime.Now.Hour:D2}:{DateTime.Now.Minute:D2}:{DateTime.Now.Second:D2}",
                    ip = ipaddr.ToString(),
                    version = ping.Version.Name,
                    currentPlayers = ping.Players.Online,
                    maxPlayers = ping.Players.Max,
                    playersOnline = users
                };

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

        static async Task WriteFileAsync(string path, string content)
        {
            using (StreamWriter outputFile = new StreamWriter(path))
            {
                await outputFile.WriteAsync(content);
            }
        }

        public static void ThrowError(string ip, string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{ip} ---- {message}");
            Console.ResetColor();
        }

        public static void ThrowError(string ip, string message, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{ip} ---- {message}: \n{ex}");
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
