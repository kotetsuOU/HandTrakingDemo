using UnityEngine;

public interface IHCD_Processor
{
    // プロセッサ名
    string ProcessorName { get; }
    
    // パイプラインの初期化時に呼ばれる
    void Setup(HCD_Pipeline pipeline);
    
    // 毎フレームディスパッチされる
    void Dispatch(ComputeBuffer pointCloudBuffer, int pointCount);
    
    // アプリケーション終了時などに呼ばれるリソース解放
    void Release();
}
