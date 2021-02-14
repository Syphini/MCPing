using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace MCPing
{
    using static Functions;
    class Client
    {
        public TcpClient client;
        private Packet readPacket;
        private byte[] buffer;

        public Client(TcpClient _client, bool server)
        {
            client = _client;
            Console.WriteLine($"Connected on: {client.Client.RemoteEndPoint}");

            readPacket = new Packet(_client.GetStream(), (client.Client.RemoteEndPoint as IPEndPoint).Address);

            if (!server)
            {
                readPacket.Write(51);
                readPacket.Flush();
            }

            buffer = new byte[short.MaxValue];
            readPacket.stream.BeginRead(buffer, 0, buffer.Length, StreamCallback, null);
        }

        public static void Start()
        {
            new Client(new TcpClient("localhost", 57105), false);
        }

        void StreamCallback(IAsyncResult result)
        {
            try
            {
                int byteLength = readPacket.stream.EndRead(result);
                if (byteLength <= 0)
                {
                    return;
                }

                int value = readPacket.ReadInt(buffer);

                Console.WriteLine(value);

                readPacket.Write(value);
                readPacket.Flush();

                readPacket.Reset();

                readPacket.stream.BeginRead(buffer, 0, buffer.Length, StreamCallback, null);
            }
            catch (Exception ex)
            {
                ThrowError(readPacket.ip, $"TCP Read Error", ex);
            }
        }
    }
}
