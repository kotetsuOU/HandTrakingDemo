using System;
using Intel.RealSense;
using UnityEngine;
using Core.Logging;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// ダミー実測点群用に特化した RsProcessingPipe の派生クラス。
    /// 本体（RsProcessingPipe.cs）の基本挙動を損なわずに、ダミー点群固有の安全なハンドリングや
    /// DebugLog のオン/オフ切り替え機能を提供します。
    /// </summary>
    [AppLoggable("DPC (Dummy Point Cloud)")]
    public class RsDummyProcessingPipe : RsProcessingPipe, IAppLoggable
    {
        public void RegisterLogTriggers(LogCategoryGroup group, System.Collections.Generic.HashSet<string> existingLabels)
        {
            var triggers = GetComponent<DPC_LogTriggers>() ?? gameObject.AddComponent<DPC_LogTriggers>();
            triggers.RegisterLogTriggers(group, existingLabels);
        }

        public void Log(string message)
        {
            AppLogger.Log(DPC_LogTriggers.TagPipe, message, this);
        }

        public void LogWarning(string message)
        {
            AppLogger.LogWarning(DPC_LogTriggers.TagPipe, message, this);
        }

        /// <summary>
        /// ダミーソースからのストリーミング開始イベントのハンドリング。
        /// ActiveProfile が null の場合（ダミーデバイス使用時）にも安全に親クラスの処理を呼び出します。
        /// </summary>
        protected override void OnSourceStart(PipelineProfile activeProfile)
        {
            Log($"Source started. ActiveProfile: {(activeProfile != null ? "RealSense Camera Profile" : "Dummy Null Profile")}");
            base.OnSourceStart(activeProfile);
        }

        protected override void OnSourceStop()
        {
            Log("Source stopped.");
            base.OnSourceStop();
        }
    }
}
