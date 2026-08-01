using UnityEngine;

namespace Core.Logging
{
    /// <summary>
    /// アプリケーション全体のモジュール別統一ログ制御API。
    /// 対象コンポーネントの Inspector を一切汚さずに、AppLogManager から一括＆個別制御が可能です。
    /// </summary>
    public static class AppLogger
    {
        public static bool IsEnabled(Object context, string subTag = null)
        {
            if (AppLogManager.Instance != null)
            {
                return AppLogManager.Instance.IsLogEnabled(context, subTag);
            }
            return false;
        }

        public static bool IsEnabled(string nameTag)
        {
            if (AppLogManager.Instance != null)
            {
                return AppLogManager.Instance.IsLogEnabled(nameTag);
            }
            return false;
        }

        public static void Log(Object context, string message)
        {
            if (!IsEnabled(context)) return;
            Debug.Log($"[{(context != null ? context.GetType().Name : "Log")}] {message}", context);
        }

        public static void Log(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? subTag : (context != null ? context.GetType().Name : "Log");
            Debug.Log($"[{prefix}] {message}", context);
        }

        public static void Log(string nameTag, string message, Object context = null)
        {
            if (!IsEnabled(nameTag)) return;
            Debug.Log($"[{nameTag}] {message}", context);
        }

        public static void LogWarning(Object context, string message)
        {
            if (!IsEnabled(context)) return;
            Debug.LogWarning($"[{(context != null ? context.GetType().Name : "Log")}] {message}", context);
        }

        public static void LogWarning(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? subTag : (context != null ? context.GetType().Name : "Log");
            Debug.LogWarning($"[{prefix}] {message}", context);
        }

        public static void LogError(Object context, string message)
        {
            if (!IsEnabled(context)) return;
            Debug.LogError($"[{(context != null ? context.GetType().Name : "Log")}] {message}", context);
        }

        public static void LogError(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? subTag : (context != null ? context.GetType().Name : "Log");
            Debug.LogError($"[{prefix}] {message}", context);
        }
    }
}

