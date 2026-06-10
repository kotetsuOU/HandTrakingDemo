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
    /// 各カメラにはCameraAdjuster.csがアタッチされている想定。
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

        [Tooltip("基準となるディスプレイを表すTransform（指定しない場合はSRDManagerのTransformを使用）")]
        public Transform displayTransform;

        [Tooltip("毎フレーム自動的にカメラ位置を視点(左目・右目)に追従させるか")]
        public bool autoTrackEyes = true;

        [Header("Mirror Settings")]
        [Tooltip("ハーフミラー環境用にトラッキング座標とフラスタムを反転させるか")]
        public bool isHalfMirrorEnabled = false;

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
        /// <param name="pattern">変更するパターン</param>
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
                    break;
            }
            
            Debug.Log($"[StereoCameraController] Camera pattern changed to: {pattern}");
        }

        /// <summary>
        /// インスペクター上で値を変更した際にも即座に反映させるための処理
        /// </summary>
        private void OnValidate()
        {
            // Playモード中のみ即座に反映させる
            if (Application.isPlaying)
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
            if (currentPattern == CameraPattern.Pattern3D && autoTrackEyes && srdManager != null)
            {
                // ネイティブプラグイン側のトラッキングキューを破壊しないよう、
                // SDKが自動更新しているアンカーから座標を読み取る
                if (_eyeAnchorL == null)
                {
                    var goL = GameObject.Find("EyeCameraAnchorL");
                    if (goL != null) _eyeAnchorL = goL.transform;
                }
                if (_eyeAnchorR == null)
                {
                    var goR = GameObject.Find("EyeCameraAnchorR");
                    if (goR != null) _eyeAnchorR = goR.transform;
                }

                if (_eyeAnchorL != null && _eyeAnchorR != null)
                {
                    Transform targetDisplay = displayTransform != null ? displayTransform : srdManager.transform;

                    // アンカーの座標はすでにワールド座標
                    Vector3 worldL = _eyeAnchorL.position;
                    Vector3 worldR = _eyeAnchorR.position;

                    // Gizmo用に生のワールド座標をキャッシュしておく
                    _debugRawL = worldL;
                    _debugRawR = worldR;

                    if (isHalfMirrorEnabled)
                    {
                        // ターゲットディスプレイのローカル座標に変換（スケール影響は逆変換で相殺される）
                        Vector3 localL = targetDisplay.InverseTransformPoint(worldL);
                        Vector3 localR = targetDisplay.InverseTransformPoint(worldR);

                        // ハーフミラーの場合は、鏡像反射により実際の移動方向(X軸)が逆転し、
                        // トラッカーが認識する左右の目も逆転するため自動補正する
                        localL.x = -localL.x;
                        localR.x = -localR.x;

                        Vector3 temp = localL;
                        localL = localR;
                        localR = temp;

                        // ワールド座標に戻す（スケール影響はここで元に戻る）
                        worldL = targetDisplay.TransformPoint(localL);
                        worldR = targetDisplay.TransformPoint(localR);
                    }

                    if (camera3DLeft != null)
                    {
                        camera3DLeft.transform.position = worldL;
                    }

                    if (camera3DRight != null)
                    {
                        camera3DRight.transform.position = worldR;
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || srdManager == null || currentPattern != CameraPattern.Pattern3D || !autoTrackEyes) return;

            // 左目(Raw)を緑のワイヤーで描画
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_debugRawL, 0.03f);
            // 右目(Raw)を赤のワイヤーで描画
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_debugRawR, 0.03f);

                // 最終的にカメラが置かれている位置（補正後）を塗りつぶしで描画
                if (camera3DLeft != null)
                {
                    Gizmos.color = new Color(0, 1, 0, 0.7f);
                    Gizmos.DrawSphere(camera3DLeft.transform.position, 0.015f);
                    // Rawからの移動先を線で結ぶ
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
