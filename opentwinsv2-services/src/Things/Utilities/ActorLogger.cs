using System;

namespace OpenTwinsV2.Things.Utilities
{
    public static class ActorLogger
    {
        public static void Info(string actorId, string message)
        {
            WriteLog("INFO", actorId, message);
        }

        public static void Warn(string actorId, string message)
        {
            WriteLog("WARN", actorId, message);
        }

        public static void Error(string actorId, string message)
        {
            WriteLog("ERROR", actorId, message);
        }

        private static void WriteLog(string level, string actorId, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"[{level.PadRight(5)}] [{timestamp}] [Actor: {actorId}] {message}");
        }
    }
}