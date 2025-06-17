using System;
using System.Collections;
using System.Threading;

namespace ArctiumStudios.SplineTools
{
    public static class Log
    {
        public static void Debug(Type type, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            if (SplineTools.LogLevel() > LogLevel.Debug) return;
            DoLog(type, log, UnityEngine.Debug.Log);
#endif
        }

        public static void Debug(object go, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            Debug(go.GetType(), log);
#endif
        }

        public static void Warn(Type type, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            if (SplineTools.LogLevel() > LogLevel.Warning) return;
            DoLog(type, log, UnityEngine.Debug.LogWarning);
#endif
        }

        public static void Warn(object go, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            Warn(go.GetType(), log);
#endif
        }

        public static void Error(Type type, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            if (SplineTools.LogLevel() > LogLevel.Error) return;
            DoLog(type, log, UnityEngine.Debug.LogError);
#endif
        }

        public static void Error(object go, Func<object> log)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            Error(go.GetType(), log);
#endif
        }

        private static void DoLog(Type type, Func<object> log, Action<object> logger)
        {
            var now = DateTime.Now.ToLongTimeString();
            logger.Invoke("[" + now + "][" + type.Name + "][" + Thread.CurrentThread.ManagedThreadId + "] " + log.Invoke());
        }

        public static string LogCollection(object toLog)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
            if (toLog == null) return "null";

            if (toLog is string) return (string) toLog;
            
            var collection = toLog as IEnumerable;
            
            if (collection != null)
            {
                var combined = "";

                foreach (var entry in collection) combined += LogCollection(entry) + ",\n";

                if (!combined.Equals("")) combined = combined.Substring(0, combined.Length - 2);

                return "[" + combined + "]";
            }

            return toLog.ToString();
#else
return null;
#endif
        }
    }
}