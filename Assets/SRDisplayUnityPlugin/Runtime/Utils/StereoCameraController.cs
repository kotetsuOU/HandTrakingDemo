using UnityEngine;

namespace SRD.Utils
{
    public enum CameraPattern
    {
        Pattern2D,
        Pattern3D
    }

    /// <summary>
    /// 2Dパターンと3Dパターンの切り替えによって、使用するカメラを制御するコントローラー。
    /// ハーフミラー環境における座標反転と視点スワップを統合管理する。
    /// </summary>
    public class StereoCameraController : MonoBehaviour
    {
        [Header("Camera References")]
        [Tooltip("2Dパターン時に使用するカメラ")]
        public Camera camera2D;

        [Tooltip("3Dパターン時に左目用として使用するカメラ")]
        public Camera camera3DLeft;

        [Tooltip("3Dパターン時に右目用として使用するカメラ")]
        public Camera camera3DRight;

        [Header("SRD Settings")]
        [Tooltip("顔トラッキング情報を取得するためのSRDManager")]
        public SRD.Core.SRDManager srdManager;
        
        [Tooltip("基準となるディスプレイを表すTransform（虚像の位置を設定）")]
        public Transform displayTransform;

        [Tooltip("毎フレーム自動的にカメラ位置を視点(左目・右目)に追従させるか")]
        public bool autoTrackEyes = true;

        [Header("Mirror Settings")]
        [Tooltip("ハーフミラー環境用にトラッキング座標を補正（スワップ＆オフセット）するか")]
        public bool isHalfMirrorEnabled = false;
        
        [Tooltip("ハーフミラー環境時などで、各軸の反転（1 または -1）を指定")]
        public Vector3 flipAxes = new Vector3(-1f, 1f, 1f);

        [Tooltip("トラッキング未検出時（初期状態）の基準となる視点位置（SRDManagerからのローカル座標）")]
        public Vector3 fallbackViewPosition = new Vector3(0f, 0.2f, -0.5f);

        [Tooltip("トラッキング未検出時（初期状態）の瞳孔間距離（IPD）")]
        public float fallbackIPD = 0.064f;

        [Header("Offset Settings")]
        [Tooltip("左目カメラに対するワールド座標系の追加オフセット")]
        public Vector3 offsetLeft = Vector3.zero;

        [Tooltip("右目カメラに対するワールド座標系の追加オフセット")]
        public Vector3 offsetRight = Vector3.zero;

        [Header("Current Mode")]
        [SerializeField]
        [Tooltip("現在のカメラパターン")]
        private CameraPattern currentPattern = CameraPattern.Pattern2D;

        public CameraPattern CurrentPattern => currentPattern;

        private void Start()
        {
            ApplyPattern(currentPattern);
        }

        /// <summary>
        /// パターンを変更し、有効なカメラを切り替える
        /// </summary>
        public void SetPattern(CameraPattern pattern)
        {
            currentPattern = pattern;
            ApplyPattern(currentPattern);
        }

        private void ApplyPattern(CameraPattern pattern)
        {
            switch (pattern)
            {
                case CameraPattern.Pattern2D:
                    if (camera2D != null) camera2D.gameObject.SetActive(true);
                    if (camera3DLeft != null) camera3DLeft.gameObject.SetActive(false);
                    if (camera3DRight != null) camera3DRight.gameObject.SetActive(false);
                    break;

                case CameraPattern.Pattern3D:
                    if (camera2D != null) camera2D.gameObject.SetActive(false);
                    if (camera3DLeft != null) camera3DLeft.gameObject.SetActive(true);
                    if (camera3DRight != null) camera3DRight.gameObject.SetActive(true);
                    
                    UpdateInternalSRDCamerasState();
                    break;
            }
            
            UnityEngine.Debug.Log($"[StereoCameraController] Camera pattern changed to: {pattern}");
        }

        private void OnValidate()
        {
            if (UnityEngine.Application.isPlaying)
            {
                ApplyPattern(currentPattern);
            }
        }

        private Vector3 _debugRawL;
        private Vector3 _debugRawR;
        private Transform _eyeAnchorL;
        private Transform _eyeAnchorR;

        private void LateUpdate()
        {
            if (currentPattern != CameraPattern.Pattern3D || !autoTrackEyes || srdManager == null) return;

            if (_eyeAnchorL == null) _eyeAnchorL = FindInHierarchy(srdManager.transform, "LeftEyeAnchor");
            if (_eyeAnchorR == null) _eyeAnchorR = FindInHierarchy(srdManager.transform, "RightEyeAnchor");
            if (_eyeAnchorL == null || _eyeAnchorR == null) return;

            // 1. SDKが認識している生の相対座標を抽出
            Vector3 nativeLocalL = srdManager.transform.InverseTransformPoint(_eyeAnchorL.position);
            Vector3 nativeLocalR = srdManager.transform.InverseTransformPoint(_eyeAnchorR.position);

            // トラッキング未検出時（初期状態の0,0,0のまま）は、フォールバック用の視点座標を適用する
            if (nativeLocalL.sqrMagnitude < 0.0001f && nativeLocalR.sqrMagnitude < 0.0001f)
            {
                nativeLocalL = fallbackViewPosition + new Vector3(-fallbackIPD / 2f, 0, 0);
                nativeLocalR = fallbackViewPosition + new Vector3(fallbackIPD / 2f, 0, 0);
            }

            Vector3 worldL;
            Vector3 worldR;

            // 2. ハーフミラー有効時、虚像ディスプレイのトランスフォームを基準に反転処理を適用
            if (isHalfMirrorEnabled && displayTransform != null)
            {
                // 2-1. 指定された軸（通常はX軸）の座標を反転し、空間全体を鏡像化する
                Vector3 flippedLocalL = new Vector3(nativeLocalL.x * flipAxes.x, nativeLocalL.y * flipAxes.y, nativeLocalL.z * flipAxes.z);
                Vector3 flippedLocalR = new Vector3(nativeLocalR.x * flipAxes.x, nativeLocalR.y * flipAxes.y, nativeLocalR.z * flipAxes.z);

                // 2-2. X軸の反転処理はCameraAdjuster側で吸収されるため、ここでは純粋に反転座標のみを適用する
                Vector3 virtualLocalL = flippedLocalL;
                Vector3 virtualLocalR = flippedLocalR;

                // 2-3. SRDManagerの傾きを維持した仮想空間行列を合成
                Matrix4x4 virtualSpaceMatrix = Matrix4x4.TRS(
                    displayTransform.position, 
                    srdManager.transform.rotation, 
                    Vector3.one
                );

                worldL = virtualSpaceMatrix.MultiplyPoint3x4(virtualLocalL);
                worldR = virtualSpaceMatrix.MultiplyPoint3x4(virtualLocalR);
            }
            else
            {
                // 通常環境（標準のトランスフォーム変換）
                worldL = srdManager.transform.TransformPoint(nativeLocalL);
                worldR = srdManager.transform.TransformPoint(nativeLocalR);
            }

            /*
            if (Time.frameCount % 60 == 0)
            {
                UnityEngine.Debug.Log($"[Tracking] nativeLocal L={nativeLocalL.ToString("F5")} R={nativeLocalR.ToString("F5")}");
                UnityEngine.Debug.Log($"[Tracking] world L={worldL.ToString("F5")} R={worldR.ToString("F5")}");
            }
            */

            _debugRawL = worldL;
            _debugRawR = worldR;

            if (camera3DLeft != null) camera3DLeft.transform.position = worldL + offsetLeft;
            if (camera3DRight != null) camera3DRight.transform.position = worldR + offsetRight;

            UpdateInternalSRDCamerasState();
        }

        private Transform FindInHierarchy(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private void UpdateInternalSRDCamerasState()
        {
            if (srdManager == null) return;
            string deviceIndex = srdManager.DeviceIndex.ToString();
            bool enableInternal = !srdManager.UseDirectGpuImageBuffer;
            
            // Find the internal cameras generated by SRDisplayUnityPlugin and update their components
            GameObject leftEye = GameObject.Find("LeftEyeCamera_" + deviceIndex);
            if (leftEye != null)
            {
                Camera cam = leftEye.GetComponent<Camera>();
                if (cam != null && cam.enabled != enableInternal) cam.enabled = enableInternal;
            }
            
            GameObject rightEye = GameObject.Find("RightEyeCamera_" + deviceIndex);
            if (rightEye != null)
            {
                Camera cam = rightEye.GetComponent<Camera>();
                if (cam != null && cam.enabled != enableInternal) cam.enabled = enableInternal;
            }
        }

        private void OnDrawGizmos()
        {
            if (!UnityEngine.Application.isPlaying || srdManager == null || currentPattern != CameraPattern.Pattern3D || !autoTrackEyes) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_debugRawL, 0.03f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_debugRawR, 0.03f);

            if (camera3DLeft != null)
            {
                Gizmos.color = new Color(0, 1, 0, 0.7f);
                Gizmos.DrawSphere(camera3DLeft.transform.position, 0.015f);
                Gizmos.DrawLine(_debugRawL, camera3DLeft.transform.position);
            }
            if (camera3DRight != null)
            {
                Gizmos.color = new Color(1, 0, 0, 0.7f);
                Gizmos.DrawSphere(camera3DRight.transform.position, 0.015f);
                Gizmos.DrawLine(_debugRawR, camera3DRight.transform.position);
            }
        }
    }
}
