using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;

namespace MCPing
{
    public class Packet
    {
        public NetworkStream stream;
        public List<byte> buffer;
        public int offset;

        public Packet(NetworkStream _stream, List<byte> _buffer)
        {
            stream = _stream;
            buffer = _buffer;
        }

        public PingPayload PingStatus(Packet packet)
        {
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


            packet.ReadVarInt(buffer);
            packet.ReadVarInt(buffer);
            var jsonLength = packet.ReadVarInt(buffer);

            //Console.WriteLine("Received packet 0x{0} with a length of {1}", packetType.ToString("X2"), length);

            var json = packet.ReadString(buffer, jsonLength);
            return JsonConvert.DeserializeObject<PingPayload>(json);
        }

        #region Read Methods
        public byte ReadByte(byte[] buffer)
        {
            var b = buffer[offset];
            offset += 1;
            return b;
        }

        public byte[] Read(byte[] buffer, int length)
        {
            var data = new byte[length];
            Array.Copy(buffer, offset, data, 0, length);
            offset += length;
            return data;
        }

        public int ReadVarInt(byte[] buffer)
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = ReadByte(buffer)) & 0x80) == 0x80)
            {
                value |= (b & 0x7F) << (size++ * 7);
                if (size > 5)
                {
                    throw new IOException("This VarInt is an imposter!");
                }
            }
            return value | ((b & 0x7F) << (size * 7));
        }

        public string ReadString(byte[] buffer, int length)
        {
            var data = Read(buffer, length);
            return Encoding.UTF8.GetString(data);
        }
        #endregion

        #region Write Methods
        public void WriteVarInt(int value)
        {
            while ((value & 128) != 0)
            {
                buffer.Add((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }
            buffer.Add((byte)value);
        }

        public void WriteShort(short value)
        {
            buffer.AddRange(BitConverter.GetBytes(value));
        }

        public void WriteString(string data)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer.Length);
            this.buffer.AddRange(buffer);
        }

        public void Write(byte b)
        {
            stream.WriteByte(b);
        }
        #endregion

        public void Flush(int id = -1)
        {
            var buffer = this.buffer.ToArray();
            this.buffer.Clear();

            var add = 0;
            var packetData = new[] { (byte)0x00 };
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = this.buffer.ToArray();
                add = packetData.Length;
                this.buffer.Clear();
            }

            WriteVarInt(buffer.Length + add);
            var bufferLength = this.buffer.ToArray();
            this.buffer.Clear();

            stream.Write(bufferLength, 0, bufferLength.Length);
            stream.Write(packetData, 0, packetData.Length);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
