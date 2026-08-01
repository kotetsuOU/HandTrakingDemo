using UnityEngine;

/// <summary>
/// HCD パイプライン内で実行される個別の計算プロセッサ（距離判定、クラスタリング等）の共通インターフェース。
/// </summary>
public interface IHCD_Processor
{
    string ProcessorName { get; }
    void Setup(HCD_Pipeline pipeline);
    void Dispatch(ComputeBuffer pointCloudBuffer, int pointCount);
    void Release();
}
