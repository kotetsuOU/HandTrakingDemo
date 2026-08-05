using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using SRD.Utils;

namespace SRD.Core
{
    public enum ProjectionReconstructMode
    {
        CustomProjectionFromCorners, // (Test 2用) 仮想カメラ位置とディスプレイ四隅から完全なOff-axis Projectionを再構築
        FrustumDeconstructAndMirror, // 行列からl,r,b,tを分解し、Frustum(l_mirrored = -r, r_mirrored = -l, b, t)で完全再構築
        FrustumDeconstructSwapLR,    // 行列からl,rを分解し、Frustum(l_mirrored = r, r_mirrored = l, b, t)でSwap再構築
        PhysicalDisplayBounds,       // SRDディスプレイの物理サイズと視点位置からFrustumを1から幾何計算
        SDKWithM02Invert,            // SDK出力行列のm02のみ符号反転
        SDKUnmodified                // SDK出力行列をそのまま使用
    }

    public enum RotationMirrorMode
    {
        MirrorRotation,      // 鏡像反転 (Vector3.Reflect による完全な3D鏡面反射)
        UnmodifiedRotation   // 実カメラのRotationを無加工で維持
    }

    [ExecuteAlways]
    public class SRDVirtualCameraTester : MonoBehaviour
    {
        [Tooltip("仮想カメラによる運動視差テストを有効にする")]
        public bool enableVirtualCamera = true;

        [Tooltip("Projection Matrixの再構築モード")]
        public ProjectionReconstructMode projectionMode = ProjectionReconstructMode.CustomProjectionFromCorners;

        [Tooltip("Rotation (回転) の反転モード")]
        public RotationMirrorMode rotationMode = RotationMirrorMode.MirrorRotation;

        private SRDManager srdManager;
        private Camera virtualLeftCamera;
        private Camera virtualRightCamera;

        private RenderTexture vCamLeftRT;
        private RenderTexture vCamRightRT;

        private Dictionary<Camera, int> originalCullingMasks = new Dictionary<Camera, int>();

        void Awake()
        {
            // Awakeで登録することで、SRDManagerやSRDEyeViewRendererよりも確実に先にendCameraRenderingをキャッチする
#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#endif
        }

        void Start()
        {
            srdManager = GetComponentInParent<SRDManager>();
            if (srdManager == null)
            {
                srdManager = FindObjectOfType<SRDManager>();
            }
        }

        void OnDestroy()
        {
#if UNITY_2019_1_OR_NEWER
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#endif
        }

        void OnDisable()
        {
            RestoreRealCameras();
            if (virtualLeftCamera) DestroyImmediate(virtualLeftCamera.gameObject);
            if (virtualRightCamera) DestroyImmediate(virtualRightCamera.gameObject);
            if (vCamLeftRT) RenderTexture.ReleaseTemporary(vCamLeftRT);
            if (vCamRightRT) RenderTexture.ReleaseTemporary(vCamRightRT);
        }

        private void RestoreRealCameras()
        {
            foreach (var kvp in originalCullingMasks)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.cullingMask = kvp.Value;
                }
            }
            originalCullingMasks.Clear();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera realCamera)
        {
            if (!enableVirtualCamera || !Application.isPlaying || srdManager == null) return;

            // 仮想カメラ自身の描画時は、Projection Matrix の m00 が負（左右反転）の場合にポリゴン表裏反転(GL.invertCulling)を適用する
            if (realCamera.name.StartsWith("Virtual") || realCamera == virtualLeftCamera || realCamera == virtualRightCamera)
            {
                GL.invertCulling = (realCamera.projectionMatrix.m00 < 0);
                return;
            }

            bool isLeft = realCamera.name.Contains("LeftEyeCamera");
            bool isRight = realCamera.name.Contains("RightEyeCamera");

            if (!isLeft && !isRight) return;

            // 目のアンカーオブジェクト (LeftEyeAnchor / RightEyeAnchor) のトランスフォームを取得
            Transform eyeAnchor = realCamera.transform.parent != null ? realCamera.transform.parent : realCamera.transform;
            Transform parentTransform = eyeAnchor.parent != null ? eyeAnchor.parent : srdManager.transform;

            // 仮想カメラの作成 (EyeAnchorと同じ親オブジェクト配下に生成する)
            Camera vCam = isLeft ? virtualLeftCamera : virtualRightCamera;
            if (vCam == null)
            {
                var go = new GameObject(isLeft ? "VirtualLeftEyeCamera" : "VirtualRightEyeCamera");
                go.transform.SetParent(parentTransform);
                vCam = go.AddComponent<Camera>();
                
                if (isLeft) virtualLeftCamera = vCam;
                else virtualRightCamera = vCam;
            }
            else if (vCam.transform.parent != parentTransform)
            {
                vCam.transform.SetParent(parentTransform);
            }

            vCam.gameObject.SetActive(true);

            // 元のカリングマスクを保存
            if (!originalCullingMasks.ContainsKey(realCamera))
            {
                originalCullingMasks[realCamera] = realCamera.cullingMask;
            }

            // 仮想カメラに実カメラの設定をコピー
            vCam.CopyFrom(realCamera);
            vCam.cullingMask = originalCullingMasks[realCamera];
            vCam.depth = realCamera.depth; // 実カメラと同じ描画優先度で適切にレンダーパイプラインを処理

            // URP 追加カメラデータ (UniversalAdditionalCameraData) の同期と深度/カラーテクスチャ要求の設定
            var realAddData = realCamera.GetComponent<UniversalAdditionalCameraData>();
            var vAddData = vCam.GetComponent<UniversalAdditionalCameraData>();
            if (vAddData == null)
            {
                vAddData = vCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (vAddData != null)
            {
                vAddData.requiresDepthOption = CameraOverrideOption.On;
                vAddData.requiresColorOption = CameraOverrideOption.On;
                if (realAddData != null)
                {
                    vAddData.renderShadows = realAddData.renderShadows;
                    vAddData.renderPostProcessing = realAddData.renderPostProcessing;
                    vAddData.antialiasing = realAddData.antialiasing;
                    vAddData.volumeLayerMask = realAddData.volumeLayerMask;
                }
            }

            // 仮想カメラ専用のRenderTextureを準備
            if (realCamera.targetTexture != null)
            {
                RenderTextureDescriptor desc = realCamera.targetTexture.descriptor;
                desc.depthBufferBits = 24; // 24-bit 明示的深度バッファを保証

                if (isLeft)
                {
                    if (vCamLeftRT == null || vCamLeftRT.width != desc.width || vCamLeftRT.height != desc.height)
                    {
                        if (vCamLeftRT) RenderTexture.ReleaseTemporary(vCamLeftRT);
                        vCamLeftRT = RenderTexture.GetTemporary(desc);
                    }
                    vCam.targetTexture = vCamLeftRT;
                }
                else
                {
                    if (vCamRightRT == null || vCamRightRT.width != desc.width || vCamRightRT.height != desc.height)
                    {
                        if (vCamRightRT) RenderTexture.ReleaseTemporary(vCamRightRT);
                        vCamRightRT = RenderTexture.GetTemporary(desc);
                    }
                    vCam.targetTexture = vCamRightRT;
                }
            }

            // 実カメラはCullingMaskを0にして何も描画させない
            realCamera.cullingMask = 0;

            // 1. ワールド空間でのトランスフォーム座標変換
            Vector3 localPosRelToManager = srdManager.transform.InverseTransformPoint(realCamera.transform.position);
            Vector3 mirroredLocalPos = localPosRelToManager;
            mirroredLocalPos.x = -localPosRelToManager.x;

            Vector3 mirroredWorldPos = srdManager.transform.TransformPoint(mirroredLocalPos);

            // ワールド座標の反転適用
            vCam.transform.position = mirroredWorldPos;

            // SRDManager基準のローカル空間におけるオイラー角を取得
            Vector3 realLocalEuler = (Quaternion.Inverse(srdManager.transform.rotation) * realCamera.transform.rotation).eulerAngles;
            
            float pitch = realLocalEuler.x;
            float yaw = realLocalEuler.y;
            float roll = realLocalEuler.z;

            // 回転(Rotation)の適用制御 (SRDManagerの右方向ベクトルに対する完全な3D鏡面反射)
            if (rotationMode == RotationMirrorMode.MirrorRotation)
            {
                Vector3 srdRight = srdManager.transform.right;
                Vector3 mirroredForward = Vector3.Reflect(realCamera.transform.forward, srdRight);
                Vector3 mirroredUp = Vector3.Reflect(realCamera.transform.up, srdRight);

                vCam.transform.rotation = Quaternion.LookRotation(mirroredForward, mirroredUp);
            }
            else
            {
                vCam.transform.rotation = realCamera.transform.rotation;
            }

            // 2. Projection Matrix の計算・再構築
            float near = realCamera.nearClipPlane;
            float far = realCamera.farClipPlane;
            Matrix4x4 origProj = realCamera.projectionMatrix;
            Matrix4x4 finalProj = origProj;

            switch (projectionMode)
            {
                case ProjectionReconstructMode.CustomProjectionFromCorners:
                    finalProj = CalculateCustomProjectionFromCorners(vCam, near, far);
                    break;
                case ProjectionReconstructMode.FrustumDeconstructAndMirror:
                    finalProj = ReconstructFrustumByDeconstruction(origProj, near, far);
                    break;
                case ProjectionReconstructMode.FrustumDeconstructSwapLR:
                    finalProj = ReconstructFrustumBySwapLR(origProj, near, far);
                    break;
                case ProjectionReconstructMode.PhysicalDisplayBounds:
                    finalProj = CalculatePhysicalFrustum(mirroredLocalPos, near, far);
                    break;
                case ProjectionReconstructMode.SDKWithM02Invert:
                    finalProj.m02 = -origProj.m02;
                    break;
                case ProjectionReconstructMode.SDKUnmodified:
                    finalProj = origProj;
                    break;
            }

            vCam.projectionMatrix = finalProj;

            // vCamの位置(-x)におけるカメラ空間に基づいて、斜め近クリップ行列(Oblique Near Clip Matrix)を正しく再計算する
            try
            {
                var bodyBounds = srdManager.Settings.DeviceInfo.BodyBounds;
                Quaternion displayRotation = Quaternion.Euler((45.0f - ((srdManager.IsWallmountMode ? 90.0f : 45.0f) + srdManager.TiltDegree)) * Vector3.left);

                var LeftBottomPositon = Quaternion.Inverse(displayRotation) * (bodyBounds.LeftBottom / bodyBounds.ScaleFactor);
                var LeftTopPositon = Quaternion.Inverse(displayRotation) * (bodyBounds.LeftUp / bodyBounds.ScaleFactor);

                float clipPlaneOffset = Mathf.Max(Mathf.Abs(LeftTopPositon.z - (-0.025f)), 
                    SRDProjectSettings.GetMutlipleDisplayMode() == SRDProjectSettings.MultiSRDMode.SingleDisplay ? .10545f : .168f);

                var clipPlanePos = new Vector3(LeftBottomPositon.x, LeftBottomPositon.y, 
                    Mathf.Max(LeftBottomPositon.z, LeftTopPositon.z) - clipPlaneOffset) * srdManager.SRDViewSpaceScale;

                clipPlanePos = srdManager.transform.rotation * clipPlanePos + srdManager.transform.position;

                var tiltedRotation = srdManager.transform.rotation * displayRotation;
                Vector3 nearClipForward = tiltedRotation * Vector3.forward;

                vCam.projectionMatrix = SRDEyeViewRenderer.CalcObliquedNearClipProjectionMatrix(vCam, nearClipForward, clipPlanePos);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VirtualCamTest] Oblique matrix recalculation failed: " + ex.Message);
            }

            var projAfterOblique = vCam.projectionMatrix;

            // 投影行列の m00 が負数(left > right)の場合はポリゴンの裏表(Winding Order)が反転するため、GL.invertCulling を設定する
            GL.invertCulling = (vCam.projectionMatrix.m00 < 0);

            // ログ出力 (SRDManager視点 / ローカル空間でのTransformデバッグ ＋ Projection / Frustum 6要素)
            if (Time.frameCount % 60 == 0 && isLeft)
            {
                Vector3 realLocalPos = srdManager.transform.InverseTransformPoint(realCamera.transform.position);
                Quaternion realLocalRot = Quaternion.Inverse(srdManager.transform.rotation) * realCamera.transform.rotation;

                Vector3 vLocalPos = srdManager.transform.InverseTransformPoint(vCam.transform.position);
                Quaternion vLocalRot = Quaternion.Inverse(srdManager.transform.rotation) * vCam.transform.rotation;

                Debug.Log($"[VirtualCamTest] デバッグ情報:\n" +
                          $"■ RealCam (Managerローカル):\n" +
                          $"  Pos: {realLocalPos}\n" +
                          $"  Rot (Euler): {realLocalRot.eulerAngles}\n" +
                          $"  {FormatFrustumAndMatrix(origProj, near, far)}\n" +
                          $"■ VirtualCam (Managerローカル):\n" +
                          $"  Pos: {vLocalPos}\n" +
                          $"  Rot (Euler): {vLocalRot.eulerAngles}\n" +
                          $"  {FormatFrustumAndMatrix(vCam.projectionMatrix, near, far)}\n" +
                          $"■ Mode: Proj={projectionMode}, Rot={rotationMode}");
            }
        }

        private string FormatFrustumAndMatrix(Matrix4x4 p, float near, float far)
        {
            float m00 = p.m00, m11 = p.m11, m02 = p.m02, m12 = p.m12;
            float r = (Mathf.Abs(m00) > 0.0001f) ? (near / m00) * (1.0f + m02) : 0;
            float l = (Mathf.Abs(m00) > 0.0001f) ? (near / m00) * (m02 - 1.0f) : 0;
            float t = (Mathf.Abs(m11) > 0.0001f) ? (near / m11) * (1.0f + m12) : 0;
            float b = (Mathf.Abs(m11) > 0.0001f) ? (near / m11) * (m12 - 1.0f) : 0;

            return $"Frustum 6要素: [left={l:F4}, right={r:F4}, bottom={b:F4}, top={t:F4}, near={near:F3}, far={far:F1}]\n" +
                   $"  Matrix: m00={p.m00:F4}, m11={p.m11:F4}, m02={p.m02:F4}, m12={p.m12:F4}, m03={p.m03:F4}, m13={p.m13:F4}";
        }

        // --- Test 2: 完全自作のOff-axis Projection ---
        private Matrix4x4 CalculateCustomProjectionFromCorners(Camera vCam, float near, float far)
        {
            var bodyBounds = srdManager.Settings.DeviceInfo.BodyBounds;
            float halfWidth = bodyBounds.Width * 0.5f;
            float halfHeight = bodyBounds.Height * 0.5f;

            // ディスプレイ平面のワールド姿勢を計算
            Quaternion displayRotation = Quaternion.Euler((45.0f - ((srdManager.IsWallmountMode ? 90.0f : 45.0f) + srdManager.TiltDegree)) * Vector3.left);
            Quaternion worldDisplayRot = srdManager.transform.rotation * displayRotation;
            Vector3 displayCenter = srdManager.transform.position;

            // ディスプレイ四隅のワールド座標
            Vector3 bl = displayCenter + worldDisplayRot * new Vector3(-halfWidth, -halfHeight, 0);
            Vector3 br = displayCenter + worldDisplayRot * new Vector3(halfWidth, -halfHeight, 0);
            Vector3 tl = displayCenter + worldDisplayRot * new Vector3(-halfWidth, halfHeight, 0);

            // 仮想カメラ(vCam)のワールド位置
            Vector3 pe = vCam.transform.position;

            // ディスプレイ基底ベクトル
            Vector3 vr = (br - bl).normalized;
            Vector3 vu = (tl - bl).normalized;
            Vector3 vn = Vector3.Cross(vr, vu).normalized; // ディスプレイ法線

            // 視点から各角へのベクトル
            Vector3 va = bl - pe;
            Vector3 vb = br - pe;
            Vector3 vc = tl - pe;

            // 視点からディスプレイ面までの垂直距離
            float d = -Vector3.Dot(va, vn); 
            if (d <= 0.001f) return vCam.projectionMatrix; // 背面にある場合はフェールセーフ

            // ディスプレイ平面に投影した場合のFrustum境界 (screen-aligned)
            float l = Vector3.Dot(vr, va) * near / d;
            float r = Vector3.Dot(vr, vb) * near / d;
            float b = Vector3.Dot(vu, va) * near / d;
            float t = Vector3.Dot(vu, vc) * near / d;

            Matrix4x4 P_screen = Matrix4x4.Frustum(l, r, b, t, near, far);

            // Screen-alignedなView空間への変換行列
            Matrix4x4 M_screen_to_world = Matrix4x4.TRS(pe, worldDisplayRot, new Vector3(1, 1, -1));
            Matrix4x4 M_world_to_screen = M_screen_to_world.inverse;

            // 実際のvCamのカメラ空間への変換行列
            Matrix4x4 M_world_to_camera = vCam.worldToCameraMatrix;
            Matrix4x4 M_camera_to_world = M_world_to_camera.inverse;

            // P_final * M_world_to_camera = P_screen * M_world_to_screen
            Matrix4x4 P_final = P_screen * M_world_to_screen * M_camera_to_world;
            return P_final;
        }

        // 行列からFrustum(left, right, bottom, top)を逆算抽出し、X軸境界(l, r)を入れ替えてMatrix4x4.Frustumで再構築
        private Matrix4x4 ReconstructFrustumByDeconstruction(Matrix4x4 p, float near, float far)
        {
            float m00 = p.m00;
            float m11 = p.m11;
            float m02 = p.m02;
            float m12 = p.m12;

            if (Mathf.Abs(m00) < 0.0001f || Mathf.Abs(m11) < 0.0001f) return p;

            // 近クリップ面での境界(left, right, bottom, top)を抽出
            float r = (near / m00) * (1.0f + m02);
            float l = (near / m00) * (m02 - 1.0f);
            float t = (near / m11) * (1.0f + m12);
            float b = (near / m11) * (m12 - 1.0f);

            // X軸(左右境界)を反転
            float l_mirrored = -r;
            float r_mirrored = -l;

            return Matrix4x4.Frustum(l_mirrored, r_mirrored, b, t, near, far);
        }

        // 行列からFrustum(left, right, bottom, top)を逆算抽出後、l_mirrored = r, r_mirrored = l で符号反転させず直接Swapして再構築
        private Matrix4x4 ReconstructFrustumBySwapLR(Matrix4x4 p, float near, float far)
        {
            float m00 = p.m00;
            float m11 = p.m11;
            float m02 = p.m02;
            float m12 = p.m12;

            if (Mathf.Abs(m00) < 0.0001f || Mathf.Abs(m11) < 0.0001f) return p;

            // 近クリップ面での境界(left, right, bottom, top)を抽出
            float r = (near / m00) * (1.0f + m02);
            float l = (near / m00) * (m02 - 1.0f);
            float t = (near / m11) * (1.0f + m12);
            float b = (near / m11) * (m12 - 1.0f);

            // X軸(左右境界)をそのまま入れ替え (l_mirrored = r, r_mirrored = l)
            float l_mirrored = r;
            float r_mirrored = l;

            return Matrix4x4.Frustum(l_mirrored, r_mirrored, b, t, near, far);
        }

        // SRDディスプレイの物理サイズと視点座標から完全に1からFrustumを幾何計算して構築
        private Matrix4x4 CalculatePhysicalFrustum(Vector3 mirroredLocalPos, float near, float far)
        {
            var bodyBounds = srdManager.Settings.DeviceInfo.BodyBounds;
            float halfWidth = bodyBounds.Width * 0.5f;
            float halfHeight = bodyBounds.Height * 0.5f;

            float eyeX = mirroredLocalPos.x;
            float eyeY = mirroredLocalPos.y;
            float eyeZ = Mathf.Abs(mirroredLocalPos.z);
            if (eyeZ < 0.001f) eyeZ = 0.5f;

            float scale = near / eyeZ;

            float l = (-halfWidth - eyeX) * scale;
            float r = (halfWidth - eyeX) * scale;
            float b = (-halfHeight - eyeY) * scale;
            float t = (halfHeight - eyeY) * scale;

            return Matrix4x4.Frustum(l, r, b, t, near, far);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera realCamera)
        {
            if (!enableVirtualCamera || !Application.isPlaying || srdManager == null) return;
            if (realCamera.name.StartsWith("Virtual") || realCamera == virtualLeftCamera || realCamera == virtualRightCamera)
            {
                GL.invertCulling = false;
                return;
            }

            bool isLeft = realCamera.name.Contains("LeftEyeCamera");
            bool isRight = realCamera.name.Contains("RightEyeCamera");

            if (!isLeft && !isRight) return;

            // 実カメラの描画が直前に終わったタイミング（SRDのホモグラフィ処理が走る直前）で
            // 仮想カメラが描画したRenderTextureを実カメラのtargetTextureへBlitコピーする！
            RenderTexture src = isLeft ? vCamLeftRT : vCamRightRT;
            if (src != null && realCamera.targetTexture != null)
            {
                Graphics.Blit(src, realCamera.targetTexture);
            }

            // カリング反転設定を標準状態(false)に復元
            GL.invertCulling = false;
        }
    }
}
