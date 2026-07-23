// =============================================================================
// IPCDPipelineStage.cs
// -----------------------------------------------------------------------------
// オクルージョンパイプラインの個々の処理ステージを表すインターフェース。
// =============================================================================
using UnityEngine.Rendering;

/// <summary>
/// オクルージョンパイプラインの1つの処理ステージ。
/// </summary>
internal interface IPCDPipelineStage
{
    /// <summary> このステージを実行すべきかどうかを判定する。 </summary>
    bool ShouldExecute(PCDPipelineContext ctx);

    /// <summary> CommandBuffer にコマンドを積んでGPU処理を発行する。 </summary>
    void Execute(CommandBuffer cmd, PCDPipelineContext ctx);
}
