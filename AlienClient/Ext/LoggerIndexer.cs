using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;

namespace AlienClient.Ext
{
    public static class LoggerIndexer
    {
        private static readonly ConcurrentDictionary<string, int> index = new ConcurrentDictionary<string, int>();
        public static ILogger GetCurrentClassLogger(string customName = null)
        {
            var loggerName = customName ?? GetClassFullName();
            var loggerIntanceIndex = index.AddOrUpdate(loggerName, 0, (name, current) => current + 1);
            return LogManager.GetLogger(loggerName + $"[{loggerIntanceIndex}]");
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetClassFullName()
        {
            int skipFrames = 2;
            string empty = string.Empty;
            string str;
            do
            {
                MethodBase method = new StackFrame(skipFrames, false).GetMethod();
                Type declaringType = method.DeclaringType;
                if (declaringType == (Type) null)
                {
                    str = method.Name;
                    break;
                }
                ++skipFrames;
                str = declaringType.FullName;
            }
            while (str.StartsWith("System.", StringComparison.Ordinal));
            return str;
        }
    }
}