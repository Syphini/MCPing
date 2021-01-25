using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPing
{
    public static class Functions
    {
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

        public static int CountIPs(string startIP, string endIP)
        {
            int[] start = ConvertIP(startIP);
            int[] end = ConvertIP(endIP);

            int[] result = new int[4]
            {
                end[0] - start[0],
                end[1] - start[1],
                end[2] - start[2],
                end[3] - start[3]
            };

            Console.WriteLine($"{result[0]}.{result[1]}.{result[2]}.{result[3]}");

            //multiply by 256

            return 0;
        }

        public static List<string> CalculateRange(string startIP, string endIP)
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

                if (start[2] == 256)
                {
                    start[1]++;
                    start[2] = 0;
                }

                if (start[0] != 256 && start[1] != 256 && start[2] != 256)
                {
                    list.Add($"{start[0]}.{start[1]}.{start[2]}.{start[3]}");
                }

                if (start[0] == end[0] && start[1] == end[1] && start[2] == end[2] && start[3] == end[3])
                    return list;

            }
            while (start[1] < 256 && start[1] < (end[1] + 1));

            return list;
        }

        public static int[] ConvertIP(string ip)
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
    }
}
