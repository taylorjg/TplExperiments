using System;
using System.Diagnostics;

namespace TplTests
{
    internal static class MyDebug
    {
        public static void Log(string format, params object[] args)
        {
            var timestamp = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss.fff");
            var managedThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var message = string.Format("[{0}, {1,4}] ", timestamp, managedThreadId);
            message += string.Format(format, args);
            Debug.WriteLine(message);
        }
    }
}
