using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPing
{
    public class Packet
    {
        public NetworkStream stream;
        public List<byte> bufferList;
        public int offset;
        public string ip;

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

        public Packet(NetworkStream _stream, List<byte> _buffer, string _ip)
        {
            stream = _stream;
            bufferList = _buffer;
            ip = _ip;
        }

        public PingPayload PingStatus(Packet packet)
        {
            //Console.WriteLine("Sending status request");

            //Send a "Handshake" packet
            packet.WriteVarInt(754);
            packet.WriteString(packet.ip);
            packet.WriteShort(25565);
            packet.WriteVarInt(1);
            packet.Flush(0);

            //Send a "Status Request" packet
            packet.Flush(0);

            #region Read Data
            byte[] buffer = new byte[short.MaxValue];
            stream.Read(buffer, 0, buffer.Length);

            var length = packet.ReadVarInt(buffer);
            var packetType = packet.ReadVarInt(buffer);

            ServerPing.ThrowError(ip, $"Received packet 0x{packetType:X2} with a length of {length}");

            var jsonLength = packet.ReadVarInt(buffer);
            #endregion

            string json = "";
            try
            {
                json = packet.ReadString(buffer, jsonLength);
                ServerPing.ThrowError(ip, json.Length.ToString());

                if (json != null)
                {
                    return JsonConvert.DeserializeObject<PingPayload>(json);
                }

                ServerPing.ThrowError(packet.ip, "Null Object");
                return ErrorPayload();
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException)
                {
                    ServerPing.ThrowError(packet.ip, "Serialization Error", ex);
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
                    ServerPing.ThrowError(packet.ip, "Returning Error Payload", ex);
                }

                return ErrorPayload();
            }
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

        public void WriteShort(short value)
        {
            bufferList.AddRange(BitConverter.GetBytes(value));
        }

        public void WriteString(string data)
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

        public void Flush(int id = -1)
        {
            var buffer = this.bufferList.ToArray();
            this.bufferList.Clear();

            var add = 0;
            var packetData = new[] { (byte)0x00 };
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = this.bufferList.ToArray();
                add = packetData.Length;
                this.bufferList.Clear();
            }

            WriteVarInt(buffer.Length + add);
            var bufferLength = this.bufferList.ToArray();
            this.bufferList.Clear();

            stream.Write(bufferLength, 0, bufferLength.Length);
            stream.Write(packetData, 0, packetData.Length);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
