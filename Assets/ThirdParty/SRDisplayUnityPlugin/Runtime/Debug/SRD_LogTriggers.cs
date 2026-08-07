using System;
using UnityEngine;

namespace SRD.Core
{
    /// <summary>
    /// 互換性のための非推奨クラス。
    /// 実際のデバッグログ出力および AppLogManager 登録処理は SRDMirrorDebugLogger に完全統一されました。
    /// </summary>
    [Obsolete("SRD_LogTriggers は非推奨です。代わりに SRDMirrorDebugLogger を使用してください。")]
    public class SRD_LogTriggers : SRDMirrorDebugLogger
    {
    }
}
