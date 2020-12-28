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
        private Packet packet;
        private byte[] buffer;

        public Client(TcpClient _client)
        {
            client = _client;

            packet = new Packet(_client.GetStream(), (client.Client.RemoteEndPoint as IPEndPoint).Address);

            buffer = new byte[short.MaxValue];
            packet.stream.BeginRead(buffer, 0, buffer.Length, StreamCallback, null);
        }

        void StreamCallback(IAsyncResult result)
        {
            try
            {
                int byteLength = packet.stream.EndRead(result);
                if (byteLength <= 0)
                {
                    return;
                }

                int value = packet.ReadInt(buffer);

                Console.WriteLine(value);

                packet.Write(value);
                packet.Flush();

                packet.stream.BeginRead(buffer, 0, buffer.Length, StreamCallback, null);
            }
            catch (Exception ex)
            {
                ThrowError(packet.ip, $"TCP Read Error", ex);
            }
        }
    }
}
