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
    }
}
