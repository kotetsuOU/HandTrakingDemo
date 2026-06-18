using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ディスプレイ（視面）の位置とサイズを利用して、カメラの非対称な投影行列(Frustum Matrix)を計算し、
/// ハーフミラー等の環境に合わせた視点（パースペクティブ）をLateUpdateで動的に調整するクラス。
/// </summary>
public class CameraAdjuster : MonoBehaviour
{
    [Header("Target Configurations")]
    [Tooltip("制御対象のカメラ（未指定の場合は、このコンポーネントがアタッチされているCameraを使用します）")]
    public Camera targetCamera;

    [Tooltip("基準となるディスプレイを表すTransform（位置とスケールから描画領域を計算）")]
    public Transform displayTransform;

    [Header("Synchronization Settings")]
    [Tooltip("このGameObject(this.transform)の位置を制御対象カメラに同期するかどうか")]
    public bool syncPosition = true;

    [Tooltip("このGameObject(this.transform)の回転を制御対象カメラに同期するかどうか")]
    public bool syncRotation = true;

    [Header("Debug")]
    [Tooltip("ハーフミラー環境用に左右のフラスタム（投影）を反転するかどうか")]
    public bool isHalfMirrorEnabled = true;

    [Tooltip("手動でプロジェクション行列（視錐台）を計算するかどうか。Falseの場合、SDKの行列をそのまま使用し、反転処理のみ適用します。")]
    public bool calculateProjectionMatrix = true;

    [Tooltip("MoveToDefaultPosition()で移動させる際のデフォルト座標")]
    public Vector3 defaultPosition = new Vector3(0.3f, 0.85f, 0.15f);

    // キャッシュ用カメラ参照
    private Camera _activeCamera;

    private void Awake()
    {
        InitializeCamera();
    }

    private void Start()
    {
        // 念のため、他のプラグイン（SRD SDKなど）がコールバックを登録した「後」に
        // こちらのコールバックが呼ばれるように、一度解除して再登録し、実行順を最後尾に回します。
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void Reset()
    {
        // アタッチ時に自動的に同一GameObjectのCameraを設定する
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }

    private void OnValidate()
    {
        // エディタの非再生時に GetComponent を呼ぶと、
        // Unityエディタ（特にプレハブインスペクター）のバグにより
        // SerializedObjectNotCreatableException が発生するケースがあるため、
        // Playモード時のみ再初期化を行うようにします。
        if (Application.isPlaying)
        {
            InitializeCamera();
        }
    }

    private void InitializeCamera()
    {
        _activeCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        if (_activeCamera != null)
        {
            _activeCamera.ResetProjectionMatrix();
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_activeCamera == null)
        {
            return;
        }

        if (isHalfMirrorEnabled && camera == _activeCamera)
        {
            GL.invertCulling = true;

            // If we are relying on the SDK's projection matrix but still need the half-mirror flip,
            // we manually invert the X-axis of the projection matrix here.
            if (!calculateProjectionMatrix)
            {
                Matrix4x4 p = camera.projectionMatrix;
                // Multiply by a scale matrix (-1, 1, 1) on the left to invert the X-axis
                p.m00 = -p.m00;
                p.m01 = -p.m01;
                p.m02 = -p.m02;
                p.m03 = -p.m03;
                camera.projectionMatrix = p;
            }
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (GL.invertCulling)
        {
            GL.invertCulling = false;
        }
    }

    private void LateUpdate()
    {
        if (_activeCamera == null || displayTransform == null)
        {
            return;
        }

        // --- 自身の位置・回転をカメラのTransformに同期 ---
        if (_activeCamera.transform != this.transform)
        {
            if (syncPosition)
            {
                _activeCamera.transform.position = this.transform.position;
            }
            if (syncRotation)
            {
                _activeCamera.transform.rotation = this.transform.rotation;
            }
        }

        // --- ディスプレイの四隅の座標をワールド空間で計算 ---
        if (calculateProjectionMatrix)
        {
            Vector3 displayCenter = displayTransform.position;
            
            // ディスプレイの傾きに対応するため、displayTransformのローカル軸（right, forward）を使用します
            Vector3 displayRight = displayTransform.right * displayTransform.localScale.x / 2;
            Vector3 displayUp = displayTransform.forward * displayTransform.localScale.z / 2;

            // ボトムレフト、ボトムライト、トップレフトの座標
            Vector3 bl = displayCenter - displayRight - displayUp;
            Vector3 br = displayCenter + displayRight - displayUp;
            Vector3 tl = displayCenter - displayRight + displayUp;

            // --- ワールド座標をカメラのローカル座標(ビュー空間)に変換 ---
            Matrix4x4 cameraTransform = _activeCamera.worldToCameraMatrix;
            bl = cameraTransform.MultiplyPoint(bl);
            br = cameraTransform.MultiplyPoint(br);
            tl = cameraTransform.MultiplyPoint(tl);

            // --- カメラのニアクリップ面(Near Plane)でのディスプレイ投影サイズを計算 ---
            float nearPlane = _activeCamera.nearClipPlane;
            float farPlane = _activeCamera.farClipPlane;

            // 相似比を利用して、ディスプレイ面のZ距離(-z)からニアクリップ面上のx,yサイズを求める
            // Z距離が0に近い場合や裏側にある場合は計算しない
            if (bl.z >= -0.001f || br.z >= -0.001f || tl.z >= -0.001f)
            {
                return;
            }

            float right = br.x * (nearPlane / -br.z);
            float left = bl.x * (nearPlane / -bl.z);
            float top = tl.y * (nearPlane / -tl.z);
            float bottom = bl.y * (nearPlane / -bl.z);

            // --- 非対称な投影行列を構築してカメラに適用 ---
            // 計算結果がNaNや無限大にならないかチェック
            if (float.IsNaN(right) || float.IsNaN(left) || float.IsNaN(top) || float.IsNaN(bottom) ||
                float.IsInfinity(right) || float.IsInfinity(left) || float.IsInfinity(top) || float.IsInfinity(bottom))
            {
                return;
            }

            Matrix4x4 p;
            if (isHalfMirrorEnabled)
            {
                // ハーフミラーの場合、左右の端を反転させる (right, left of frustum)
                p = Matrix4x4.Frustum(right, left, bottom, top, nearPlane, farPlane);
            }
            else
            {
                // 通常の場合
                p = Matrix4x4.Frustum(left, right, bottom, top, nearPlane, farPlane);
            }

            _activeCamera.projectionMatrix = p;
        }
    }

    /// <summary>
    /// カメラ（または視点）の位置を、インスペクターで指定した defaultPosition に強制的に移動させる
    /// </summary>
    public void MoveToDefaultPosition()
    {
        this.transform.position = defaultPosition;

        UnityEngine.Debug.Log($"Moved to default position: {defaultPosition}");
    }
}