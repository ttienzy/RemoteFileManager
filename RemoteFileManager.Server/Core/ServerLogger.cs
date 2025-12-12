using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Server.Core
{
    public static class ServerLogger
    {
        public static void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} INFO] {message}");
            Console.ResetColor();
        }
        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} WARN] {message}");
            Console.ResetColor();
        }

        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss} ERROR] {message}");
            Console.ResetColor();
        }
    }
}
