using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Logging
{
    /// <summary>
    /// アプリケーションログの重要度レベル
    /// </summary>
    public enum AppLogLevel
    {
        Info = 0,     // 通常ログ (AppLogger.Log)
        Warning = 1,  // 警告ログ (AppLogger.LogWarning)
        Error = 2     // エラーログ (AppLogger.LogError)
    }

    /// <summary>
    /// AppLoggerログ管理対象コンポーネントであることを明示する属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class AppLoggableAttribute : Attribute
    {
        public string CategoryName { get; }
        public AppLoggableAttribute(string categoryName = null)
        {
            CategoryName = categoryName;
        }
    }

    /// <summary>
    /// コンポーネントが自前で複数のサブログトリガーをAppLogManagerへ登録するためのインターフェース
    /// </summary>
    public interface IAppLoggable
    {
        void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels);
    }

    /// <summary>
    /// アプリケーション全体のモジュール別統一ログ制御API。
    /// 対象コンポーネントの Inspector を一切汚さずに、AppLogManager から一括＆個別制御が可能です。
    /// </summary>
    public static class AppLogger
    {
        public static bool IsEnabled(Object context, string subTag = null)
        {
            return IsEnabled(context, AppLogLevel.Info, subTag);
        }

        public static bool IsEnabled(Object context, AppLogLevel level, string subTag = null)
        {
            if (AppLogManager.Instance != null)
            {
                return AppLogManager.Instance.IsLogEnabled(context, level, subTag);
            }
            return false;
        }

        public static bool IsEnabled(string nameTag, AppLogLevel level = AppLogLevel.Info)
        {
            if (AppLogManager.Instance != null)
            {
                return AppLogManager.Instance.IsLogEnabled(nameTag, level);
            }
            return false;
        }

        public static void Log(Object context, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Info)) return;
            Debug.Log($"[{GetContextPrefix(context)}] {message}", context);
        }

        public static void Log(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Info, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? $"{GetContextPrefix(context)} > {subTag}" : GetContextPrefix(context);
            Debug.Log($"[{prefix}] {message}", context);
        }

        public static void Log(string nameTag, string message, Object context = null)
        {
            if (!IsEnabled(nameTag, AppLogLevel.Info)) return;
            Debug.Log($"[{nameTag}] {message}", context);
        }

        public static void LogWarning(Object context, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Warning)) return;
            Debug.LogWarning($"[{GetContextPrefix(context)}] {message}", context);
        }

        public static void LogWarning(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Warning, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? $"{GetContextPrefix(context)} > {subTag}" : GetContextPrefix(context);
            Debug.LogWarning($"[{prefix}] {message}", context);
        }

        public static void LogWarning(string nameTag, string message, Object context = null)
        {
            if (!IsEnabled(nameTag, AppLogLevel.Warning)) return;
            Debug.LogWarning($"[{nameTag}] {message}", context);
        }

        public static void LogError(Object context, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Error)) return;
            Debug.LogError($"[{GetContextPrefix(context)}] {message}", context);
        }

        public static void LogError(Object context, string subTag, string message)
        {
            if (!IsEnabled(context, AppLogLevel.Error, subTag)) return;
            string prefix = !string.IsNullOrEmpty(subTag) ? $"{GetContextPrefix(context)} > {subTag}" : GetContextPrefix(context);
            Debug.LogError($"[{prefix}] {message}", context);
        }

        public static void LogError(string nameTag, string message, Object context = null)
        {
            if (!IsEnabled(nameTag, AppLogLevel.Error)) return;
            Debug.LogError($"[{nameTag}] {message}", context);
        }

        private static string GetContextPrefix(Object context)
        {
            if (context == null) return "Log";
            if (context is Component comp && comp != null)
            {
                return $"{comp.GetType().Name}: {comp.gameObject.name}";
            }
            if (context is GameObject go && go != null)
            {
                return go.name;
            }
            return context.GetType().Name;
        }
    }
}

