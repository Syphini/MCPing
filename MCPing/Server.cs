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
        static TcpListener listener;

        const int port = 2222;

        public static void Start()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            listener.BeginAcceptTcpClient(ConnectCallback, null);
        }

        static void ConnectCallback(IAsyncResult result)
        {

            Client client = new Client(listener.EndAcceptTcpClient(result));
            Console.WriteLine($"Connect on: {client.client.Client.RemoteEndPoint}");
            listener.BeginAcceptTcpClient(ConnectCallback, null);

        }


    }
}
