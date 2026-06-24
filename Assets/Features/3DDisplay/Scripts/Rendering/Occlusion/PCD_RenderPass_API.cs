// =============================================================================
// PCD_RenderPass_API.cs
// -----------------------------------------------------------------------------
// 外部（他のコンポーネントやシステム）からのデータ入力や設定変更を受け付ける
// パブリック API をまとめた partial クラス。
// =============================================================================
using UnityEngine;

public partial class PCDRenderPass
{
    // =========================================================================
    // 設定の更新
    // =========================================================================

    /// <summary> 外部（スクリプトやインスペクターの変更など）からレンダラーの設定を更新します。 </summary>
    public void UpdateSettings(PCDRendererFeature.PCDRenderSettings settings)
    {
        this._settings = settings;
    }

    /// <summary> オリジンデバッグマップなどのレンダリングを切り替えます。 </summary>
    public void SetDebugFlags(bool enablePixelTagMap, bool enableOcclusionMap)
    {
        this._settings.enablePixelTagMap = enablePixelTagMap;
        this._settings.enableOcclusionMap = enableOcclusionMap;
    }

    // =========================================================================
    // バッファ操作 — PCDPointBufferManager への委譲
    // =========================================================================

    /// <summary> 外部のコンピュートバッファを直接注入できるようにします。 </summary>
    public void SetExternalBuffer(ComputeBuffer buffer, int count)
    {
        _bufferManager.SetExternalBuffer(buffer, count);
    }

    /// <summary> 内部のPCV_Dataオブジェクトから点群データを設定します。 </summary>
    public void SetPointCloudData(PCV_Data data)
    {
        _bufferManager.SetPointCloudData(data);
    }

    /// <summary> 点群のオクルージョンと相互作用するように静的なUnityメッシュを登録します。 </summary>
    public void AddStaticMesh(Mesh mesh, Transform transform, PCDProcessingMode mode)
    {
        _bufferManager.AddStaticMesh(mesh, transform, mode);
    }

    /// <summary> バッファの更新を強制するために、点群データをダーティとしてマークします。 </summary>
    public void MarkPointCloudDataDirty()
    {
        _bufferManager.SetDataDirty();
    }

    /// <summary> トラックされている静的なUnityメッシュの登録を解除します。 </summary>
    public void RemoveStaticMesh(Mesh mesh, Transform transform)
    {
        _bufferManager.RemoveStaticMesh(mesh, transform);
    }

    // =========================================================================
    // クエリ API
    // =========================================================================

    /// <summary> デバッグマップが生成されている場合はそれを返し、そうでない場合はnullを返します。 </summary>
    public Texture GetDebugDisplayMap()
    {
        if ((_settings.enablePixelTagMap || _settings.enableOcclusionMap) && _debugDisplayMapHandle != null)
        {
            return _debugDisplayMapHandle;
        }
        return null;
    }
}
