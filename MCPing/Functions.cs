using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace MCPing
{
    public static class Functions
    {
        public static void ThrowError(IPAddress ip, string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{ip} ---- {message}");
            Console.ResetColor();
        }

        public static void ThrowError(IPAddress ip, string message, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{ip} ---- {message}: \n{ex}");
            Console.ResetColor();
        }
    }
}
