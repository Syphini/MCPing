using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace MCPing
{
    class Server
    {
        static TcpListener scannerListener;
        static TcpListener clientListener;

        public delegate void PacketHandler(int fromClient, Packet packet);
        public static Dictionary<int, PacketHandler> packetHandlers;

        const int scannerPort = 57105;
        const int clientPort = 40892;

        public static void Start()
        {
            InitializeData();

            scannerListener = new TcpListener(IPAddress.Any, scannerPort);
            scannerListener.Start();

            scannerListener.BeginAcceptTcpClient(ScannerCallback, null);

            //Initilize Listener for Client (GUI) connections
            //Primary Function will be to add new IP's and grab current ServerList
            clientListener = new TcpListener(IPAddress.Any, clientPort);
            clientListener.Start();

            clientListener.BeginAcceptTcpClient(ClientCallback, null);

        }

        static void ScannerCallback(IAsyncResult result)
        {
            Client client = new Client(scannerListener.EndAcceptTcpClient(result), true);
            scannerListener.BeginAcceptTcpClient(ScannerCallback, null);
        }

        //UPDATE FOR GUI CONNECTIONS - INCL (new Client())
        static void ClientCallback(IAsyncResult result)
        {
            Client client = new Client(clientListener.EndAcceptTcpClient(result), true);
            clientListener.BeginAcceptTcpClient(ClientCallback, null);
        }

        static void InitializeData()
        {
            packetHandlers = new Dictionary<int, PacketHandler>()
            {
                //{(int)TCPPackets.welcome, }
            };
        }
    }
}
