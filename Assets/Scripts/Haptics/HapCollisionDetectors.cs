using UnityEngine;

/// <summary>
/// RsGlobalPointCloudManager で統合された点群データに対して、指定した物体やメッシュが接触しているかを計算するクラス。
/// ComputeShader を使用し、大量の点群計算（数百万点規模）でもパフォーマンスを落とさずに接触判定を行います。
/// </summary>
public class HapCollisionDetectors : MonoBehaviour
{
    public enum DetectionMode
    {
        TransformOnly,       // 単一のオブジェクト(Transform)の座標と半径で判定する
        SkinnedMeshRenderer  // アニメーションする SkinnedMeshRenderer の表面と半径で判定する
    }

    [Header("Settings")]
    [Tooltip("判定に使用するモードを選択します")]
    public DetectionMode detectionMode = DetectionMode.TransformOnly;

    [Tooltip("TransformOnly モードの際に判定基準とするオブジェクト (中心座標として使用)")]
    public Transform targetObject;

    [Tooltip("SkinnedMeshRenderer モードの際に判定基準とするターゲットメッシュ")]
    public SkinnedMeshRenderer targetSkinnedMesh;

    [Tooltip("接触判定を行う半径 (ターゲットとの距離の閾値)")]
    public float collisionRadius = 0.5f;

    [Tooltip("高速化のためのの境界箱に持たせる余白(メートル)。アニメーションの激しさによっては少し広めに取ると安定します。")]
    public float boundsPadding = 0.1f;

    [Tooltip("メッシュ表面での計算時に、検証を間引く頂点数。大きいほど計算が軽いですが精度が落ちます。(例: 10 なら10頂点ごとに計算)")]
    [Range(1, 100)]
    public int vertexSamplingStep = 10;

    [Tooltip("点群との衝突を計算するための Compute Shader ファイル")]
    public ComputeShader collisionComputeShader;

    [Header("Debug")]
    [Tooltip("Play中にインスペクタ上で現在接触しているかどうかがリアルタイムに表示されます")]
    public string debugCollisionStatus = "Not Colliding";

    [Tooltip("接触時にシーンビュー上で境界枠や球（緑/赤）をGizmoとして表示するかどうか")]
    public bool showDebugGizmo = true;

    /// <summary>
    /// 現在、点群がターゲットに接触しているかどうかを返します (True/False)
    /// </summary>
    public bool IsColliding { get; private set; }

    /// <summary>
    /// 最後に接触したメッシュ頂点のワールド座標
    /// </summary>
    public Vector3 HitPosition { get; private set; }

    /// <summary>
    /// 最後に接触したメッシュ頂点のワールド法線
    /// </summary>
    public Vector3 HitNormal { get; private set; }

    // Shader 側の HitResult 構造体に対応させる
    [System.Serializable]
    private struct HitResult
    {
        public int isColliding;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
    }

    private ComputeBuffer _resultBuffer;
    private HitResult[] _resultData = new HitResult[1];

    private int _kernelTransform;
    private int _kernelMesh;

    private Mesh _bakedMesh;
    private ComputeBuffer _meshVerticesBuffer;
    private ComputeBuffer _meshNormalsBuffer;
    private Vector3[] _meshVertices;
    private Vector3[] _meshNormals;

    private void Start()
    {
        if (collisionComputeShader != null)
        {
            _kernelTransform = collisionComputeShader.FindKernel("CheckCollision");
            _kernelMesh = collisionComputeShader.FindKernel("CheckCollisionMesh");
        }

        // int(4) + float3(12) + float3(12) = 28 bytes
        _resultBuffer = new ComputeBuffer(1, 28);
    }

    private void Update()
    {
        if (RsGlobalPointCloudManager.Instance == null || collisionComputeShader == null)
            return;

        // グローバルマネージャから統合バッファと現在の点群数を取得
        var globalBuffer = RsGlobalPointCloudManager.Instance.GetGlobalBuffer();
        int pointsCount = RsGlobalPointCloudManager.Instance.CurrentTotalCount;

        // 点群が存在しない場合は接触なしとして終了
        if (globalBuffer == null || pointsCount == 0)
        {
            UpdateCollisionResult(false);
            return;
        }

        // --- ComputeBuffer のデータリセット ---
        // 前フレームの結果が残らないように確実に 0(False) で初期化
        _resultData[0] = new HitResult { isColliding = 0, hitPoint = Vector3.zero, hitNormal = Vector3.zero };
        _resultBuffer.SetData(_resultData);

        if (detectionMode == DetectionMode.TransformOnly)
        {
            if (targetObject == null) return;

            // Shaderへのパラメータセット (TransformOnlyモード)
            collisionComputeShader.SetBuffer(_kernelTransform, "PointCloudBuffer", globalBuffer);
            collisionComputeShader.SetBuffer(_kernelTransform, "Result", _resultBuffer);
            collisionComputeShader.SetInt("PointsCount", pointsCount);
            collisionComputeShader.SetVector("TargetPosition", targetObject.position);
            collisionComputeShader.SetFloat("RadiusSqr", collisionRadius * collisionRadius);
            collisionComputeShader.SetFloat("Radius", collisionRadius);

            // スレッドグループ数を計算し、ComputeShader をディスパッチ (1グループ=256スレッド)
            int threadGroups = Mathf.CeilToInt(pointsCount / 256.0f);
            collisionComputeShader.Dispatch(_kernelTransform, threadGroups, 1, 1);
        }
        else if (detectionMode == DetectionMode.SkinnedMeshRenderer)
        {
            if (targetSkinnedMesh == null) return;

            // アニメーションなどで変形した後の現在のメッシュ形状(頂点座標)をベイクして取得
            // ※ BakeMesh で得られる頂点は対象SkinnedMeshRendererの Transform の Local 座標系になります
            if (_bakedMesh == null)
            {
                _bakedMesh = new Mesh();
            }
            targetSkinnedMesh.BakeMesh(_bakedMesh, true);

            _meshVertices = _bakedMesh.vertices;
            _meshNormals = _bakedMesh.normals;
            if (_meshVertices == null || _meshVertices.Length == 0) return;

            // 頂点・法線用の ComputeBuffer のサイズを調整
            if (_meshVerticesBuffer == null || _meshVerticesBuffer.count != _meshVertices.Length)
            {
                _meshVerticesBuffer?.Release();
                _meshNormalsBuffer?.Release();
                _meshVerticesBuffer = new ComputeBuffer(_meshVertices.Length, sizeof(float) * 3);
                _meshNormalsBuffer = new ComputeBuffer(_meshVertices.Length, sizeof(float) * 3);
            }

            // CPUで取得した頂点・法線配列をGPU側へ転送
            _meshVerticesBuffer.SetData(_meshVertices);

            // normals 配列が存在しない場合はエラーを防ぐためダミーを与える
            if (_meshNormals != null && _meshNormals.Length == _meshVertices.Length)
            {
                _meshNormalsBuffer.SetData(_meshNormals);
            }

            // Shaderへのパラメータセット (SkinnedMeshモード)
            collisionComputeShader.SetBuffer(_kernelMesh, "PointCloudBuffer", globalBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "Result", _resultBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "MeshVerticesBuffer", _meshVerticesBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "MeshNormalsBuffer", _meshNormalsBuffer);

            collisionComputeShader.SetInt("PointsCount", pointsCount);
            collisionComputeShader.SetInt("MeshVerticesCount", _meshVertices.Length);
            collisionComputeShader.SetMatrix("LocalToWorldMatrix", targetSkinnedMesh.transform.localToWorldMatrix);

            // Broad-phase 判定用: メッシュ全体を囲む境界箱(Bounds)。
            // Renderer.bounds はワールド座標系でのバウンディングボックスを直接返してくれます。
            // 計算量を減らすため、点群がこの境界箱の中に入っていない場合はそもそも演算をスキップします。
            Bounds bounds = targetSkinnedMesh.bounds;

            // collisionRadiusに加えて、ユーザー指定の余白を持たせて安全マージンを確保
            float totalPadding = collisionRadius + boundsPadding;

            collisionComputeShader.SetVector("MeshBoundsMin", bounds.min - new Vector3(totalPadding, totalPadding, totalPadding));
            collisionComputeShader.SetVector("MeshBoundsMax", bounds.max + new Vector3(totalPadding, totalPadding, totalPadding));

            collisionComputeShader.SetFloat("Radius", collisionRadius);
            collisionComputeShader.SetFloat("RadiusSqr", collisionRadius * collisionRadius);
            collisionComputeShader.SetInt("VertexSubstep", vertexSamplingStep);

            int threadGroups = Mathf.CeilToInt(pointsCount / 256.0f);
            collisionComputeShader.Dispatch(_kernelMesh, threadGroups, 1, 1);
        }

        // --- ComputeShader の実行結果を受け取る ---
        _resultBuffer.GetData(_resultData);
        bool col = _resultData[0].isColliding > 0;

        UpdateCollisionResult(col);

        if (col)
        {
            HitPosition = _resultData[0].hitPoint;
            HitNormal = _resultData[0].hitNormal;
        }
    }

    /// <summary>
    /// 衝突ステータスの更新と、デバッグ表示の反映を行います
    /// </summary>
    private void UpdateCollisionResult(bool isColliding)
    {
        IsColliding = isColliding;

        // インスペクタ等で状態を確認しやすくするための表示更新
        if (IsColliding)
        {
            debugCollisionStatus = "🔥 COLLIDING! (接触中)";
        }
        else
        {
            debugCollisionStatus = "Not Colliding...";
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugGizmo) return;

        // 接触時は赤色、非接触時は緑色の半透明で境界や対象を描画します
        Gizmos.color = IsColliding ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);

        if (detectionMode == DetectionMode.TransformOnly && targetObject != null)
        {
            Gizmos.DrawWireSphere(targetObject.position, collisionRadius);
            Gizmos.color = IsColliding ? new Color(1f, 0f, 0f, 0.2f) : new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(targetObject.position, collisionRadius);
        }
        else if (detectionMode == DetectionMode.SkinnedMeshRenderer && targetSkinnedMesh != null)
        {
            Bounds bounds = targetSkinnedMesh.bounds;
            bounds.Expand(collisionRadius * 2f); 
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.color = IsColliding ? new Color(1f, 0f, 0f, 0.2f) : new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        // 接触位置と法線をGizmoで可視化する (Rayが刺さったような表現)
        if (IsColliding)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(HitPosition, 0.05f); // 接触した場所に小さな球体
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(HitPosition, HitNormal * 0.3f); // 法線方向に線を伸ばす
        }
    }
#endif

    private void OnDestroy()
    {
        // 確保した ComputeBuffer などのネイティブリソースはメモリリーク防止のために必ず Release します
        _resultBuffer?.Release();
        _meshVerticesBuffer?.Release();
        _meshNormalsBuffer?.Release();
        if (_bakedMesh != null)
        {
            Destroy(_bakedMesh);
        }
    }
}
