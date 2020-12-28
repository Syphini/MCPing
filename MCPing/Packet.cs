using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

namespace MCPing
{
    public class Packet
    {
        public NetworkStream stream;
        public List<byte> bufferList;
        public int offset;
        public IPAddress ip;

        public Packet(NetworkStream _stream)
        {
            stream = _stream;
            bufferList = new List<byte>();
        }

        public Packet(NetworkStream _stream, IPAddress _ip)
        {
            stream = _stream;
            bufferList = new List<byte>();
            ip = _ip;
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

        public int ReadInt(byte[] buffer)
        {
            int value = BitConverter.ToInt32(buffer, offset);
            offset += 4;
            return value;
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
                bufferList.Add((byte)(value & 127 | 128));
                value = (int)((uint)value) >> 7;
            }
            bufferList.Add((byte)value);
        }

        public void Write(short value)
        {
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(int value)
        {
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        public void Write(string data)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer.Length);
            this.bufferList.AddRange(buffer);
        }

        public void Write(byte b)
        {
            stream.WriteByte(b);
        }
        #endregion

        public void MCFlush(int id = -1)
        {
            var buffer = bufferList.ToArray();
            bufferList.Clear();

            var add = 0;
            var packetData = new[] { (byte)0x00 };
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = bufferList.ToArray();
                add = packetData.Length;
                bufferList.Clear();
            }

            WriteVarInt(buffer.Length + add);
            var bufferLength = bufferList.ToArray();
            bufferList.Clear();

            stream.Write(bufferLength, 0, bufferLength.Length);
            stream.Write(packetData, 0, packetData.Length);
            stream.Write(buffer, 0, buffer.Length);
        }

        public void Flush()
        {
            byte[] buffer = bufferList.ToArray();
            bufferList.Clear();

            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
