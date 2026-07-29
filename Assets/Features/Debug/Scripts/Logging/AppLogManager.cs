// =============================================================================
// AppLogManager.cs
// -----------------------------------------------------------------------------
// アプリケーション全体およびカテゴリごとのログを一斉・集中管理するスタティッククラス。
// 個々のコンポーネントの enableLog とグローバル/カテゴリ別のON/OFFスイッチを組み合わせ、
// 容易な一括制御を実現します。
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace RealTimeOcclusion.Logging
{
    public static class AppLogManager
    {
        // グローバルログマスターマスター管理スイッチ
        public static bool GlobalEnableLog { get; set; } = true;

        // カテゴリ別個別制御ディクショナリ
        private static readonly Dictionary<AppLogCategory, bool> _categoryStates = new Dictionary<AppLogCategory, bool>();

        static AppLogManager()
        {
            // 全カテゴリデフォルトは有効
            foreach (AppLogCategory cat in System.Enum.GetValues(typeof(AppLogCategory)))
            {
                _categoryStates[cat] = true;
            }
        }

        /// <summary>
        /// 特定カテゴリのログ出力有効化状態を設定します。
        /// </summary>
        public static void SetCategoryEnabled(AppLogCategory category, bool enabled)
        {
            _categoryStates[category] = enabled;
        }

        /// <summary>
        /// 特定カテゴリのログ出力有効化状態を取得します。
        /// </summary>
        public static bool IsCategoryEnabled(AppLogCategory category)
        {
            return _categoryStates.TryGetValue(category, out bool enabled) ? enabled : true;
        }

        /// <summary>
        /// ログが出力対象かどうかを判定します。
        /// グローバルON × カテゴリ別ON × ローカル(コンポーネント)ON
        /// </summary>
        public static bool IsLogEnabled(AppLogCategory category = AppLogCategory.General, bool localEnableLog = true)
        {
            if (!GlobalEnableLog) return false;
            if (!localEnableLog) return false;
            return IsCategoryEnabled(category);
        }

        /// <summary>
        /// 通常のログを出力します。
        /// </summary>
        public static void Log(AppLogCategory category, string message, bool localEnableLog = true, Object context = null)
        {
            if (!IsLogEnabled(category, localEnableLog)) return;
            if (context != null)
                Debug.Log(message, context);
            else
                Debug.Log(message);
        }

        /// <summary>
        /// カテゴリ省略（General）のログ出力
        /// </summary>
        public static void Log(string message, bool localEnableLog = true, Object context = null)
        {
            Log(AppLogCategory.General, message, localEnableLog, context);
        }

        /// <summary>
        /// 警告ログを出力します。
        /// </summary>
        public static void LogWarning(AppLogCategory category, string message, bool localEnableLog = true, Object context = null)
        {
            if (!IsLogEnabled(category, localEnableLog)) return;
            if (context != null)
                Debug.LogWarning(message, context);
            else
                Debug.LogWarning(message);
        }

        /// <summary>
        /// エラーログを出力します。(エラーは基本的に出力しますが、GlobalEnableLogで消すことも可能)
        /// </summary>
        public static void LogError(AppLogCategory category, string message, Object context = null)
        {
            if (!GlobalEnableLog) return;
            if (context != null)
                Debug.LogError(message, context);
            else
                Debug.LogError(message);
        }
    }
}
