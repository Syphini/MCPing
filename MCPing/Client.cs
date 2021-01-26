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

        public Client(TcpClient _client)
        {
            client = _client;

            readPacket = new Packet(_client.GetStream(), (client.Client.RemoteEndPoint as IPEndPoint).Address);

            buffer = new byte[short.MaxValue];
            readPacket.stream.BeginRead(buffer, 0, buffer.Length, StreamCallback, null);
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
