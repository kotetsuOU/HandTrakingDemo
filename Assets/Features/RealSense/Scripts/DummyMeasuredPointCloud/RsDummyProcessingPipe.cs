using System;
using Intel.RealSense;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// ダミー実測点群用に特化した RsProcessingPipe の派生クラス。
    /// 本体（RsProcessingPipe.cs）の基本挙動を損なわずに、ダミー点群固有の安全なハンドリングや
    /// DebugLog のオン/オフ切り替え機能を提供します。
    /// </summary>
    public class RsDummyProcessingPipe : RsProcessingPipe
    {
        [Header("Debug Log Settings")]
        [Tooltip("True にすると、ダミー点群パイプラインの動作ログをコンソールに出力します")]
        public bool enableDebugLog = false;

        public void Log(string message)
        {
            if (enableDebugLog)
            {
                UnityEngine.Debug.Log($"[RsDummyProcessingPipe: {gameObject.name}] {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (enableDebugLog)
            {
                UnityEngine.Debug.LogWarning($"[RsDummyProcessingPipe: {gameObject.name}] {message}");
            }
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
