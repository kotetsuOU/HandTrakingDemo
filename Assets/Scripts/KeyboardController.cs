using System;
using System.IO;
using UnityEngine;
using static PCDRendererFeature;

public class KeyboardController : MonoBehaviour
{
    [Header("Control Targets")]
    [Tooltip("アニメーションの再生/一時停止を切り替えるAnimator (現在はtoggleObjects内でアクティブなものから自動的に取得されます)")]
    private Animator targetAnimator;

    [Tooltip("Tabキーで順番に表示を切り替える関連オブジェクトの配列")]
    public GameObject[] toggleObjects;

    private int currentActiveIndex = 0;

    [Tooltip("キーボード操作で移動させる対象のオブジェクト (現在はtoggleObjects内でアクティブなものから自動的に取得されます)")]
    private Transform targetTransform;

    [Tooltip("カメラキャプチャ用スクリプト (ViewPointのカメラ映像保存用)")]
    public CameraCapture cameraCapture;

    [Tooltip("マテリアル切り替え用コントローラー")]
    public RsMaterialController materialController;

    [Tooltip("移動速度")]
    public float moveSpeed = 1.0f;

    private void Start()
    {
        UpdateActiveTargetReferences();
    }

    private void UpdateActiveTargetReferences()
    {
        if (toggleObjects != null && toggleObjects.Length > 0 && currentActiveIndex >= 0 && currentActiveIndex < toggleObjects.Length)
        {
            GameObject activeObj = toggleObjects[currentActiveIndex];
            if (activeObj != null)
            {
                targetTransform = activeObj.transform;
                targetAnimator = activeObj.GetComponent<Animator>();
            }
        }
    }

    void Update()
    {
        // ----------------------------------------------------
        // 1. QuitGame の統合 (Escapeキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        // ----------------------------------------------------
        // 2. 撮影 (Enter / Returnキー)
        // デバッグ画像と現在のViewPointカメラ映像を同時保存！
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string methodPrefix = "";

            // ① オクルージョンマップの書き出しフラグをオン
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                PCDRendererFeature.Instance.settings.recordOcclusionDebugMap = true;
                PCDRendererFeature.Instance.settings.recordPixelTagMap = true;
                PCDRendererFeature.Instance.settings.recordIntegratedDepthMap = true;
                PCDRendererFeature.Instance.settings.recordNeighborhoodMap = true;
                PCDRendererFeature.Instance.settings.recordNeighborCountMap = true;
                Debug.Log("[KeyboardController] オクルージョン関連DebugMapの出力をリクエストしました");

                bool isTag = PCDRendererFeature.Instance.settings.enableTagBasedOptimization;
                bool isDensity = PCDRendererFeature.Instance.settings.enableTypeAwareDensity;
                bool isFade = PCDRendererFeature.Instance.settings.enableSoftOcclusionFade;
                bool isHoleFill = PCDRendererFeature.Instance.settings.holeFillingMethod != PCDRendererFeature.PCV_HoleFillingMethod.None;

                if (isTag && isDensity && isFade && isHoleFill) methodPrefix = "Proposal";
                else if (!isTag && !isDensity && !isFade && !isHoleFill) methodPrefix = "Traditional";
                else methodPrefix = $"Ablation_T{(isTag?"1":"0")}_D{(isDensity?"1":"0")}_F{(isFade?"1":"0")}_H{(isHoleFill?"1":"0")}";
            }

            // ② 同時にCameraCaptureのCapture()を実行してViewPointカメラ映像を保存
            if (cameraCapture != null)
            {
                cameraCapture.Capture(methodPrefix);
            }
            else
            {
                // アタッチし忘れていた場合のフォールバック（シーン内から検索）
                CameraCapture cc = FindFirstObjectByType<CameraCapture>();
                if (cc != null)
                {
                    cc.Capture(methodPrefix);
                }
                else
                {
                    Debug.LogWarning("[KeyboardController] CameraCaptureが設定・発見されなかったため、カメラ映像の保存はスキップされました。");
                }
            }
        }

        // ----------------------------------------------------
        // 3. オブジェクトのActive順番切り替え (Tabキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (toggleObjects != null && toggleObjects.Length > 0)
            {
                // 現在のものをオフ
                if (toggleObjects[currentActiveIndex] != null)
                {
                    toggleObjects[currentActiveIndex].SetActive(false);
                }

                // インデックスを進める
                currentActiveIndex = (currentActiveIndex + 1) % toggleObjects.Length;

                // 次のものをオン
                if (toggleObjects[currentActiveIndex] != null)
                {
                    toggleObjects[currentActiveIndex].SetActive(true);
                }

                // アクティブになったオブジェクトからAnimatorとTransformを取得し直す
                UpdateActiveTargetReferences();

                Debug.Log($"[KeyController] オブジェクトのActiveを {toggleObjects[currentActiveIndex]?.name} ({currentActiveIndex}番目) に切り替えました。");
            }
            else
            {
                Debug.LogWarning("[KeyController] Inspectorで toggleObjects が設定されていません。");
            }
        }

        // ----------------------------------------------------
        // 4. アニメーションの一時停止 / 再開 (Spaceキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (targetAnimator != null)
            {
                // Animatorの再生速度を0と1でスイッチする
                targetAnimator.speed = (targetAnimator.speed > 0f) ? 0f : 1f;
                Debug.Log($"[KeyController] アニメーション: {(targetAnimator.speed > 0f ? "再生" : "停止")}");
            }
            else
            {
                Debug.LogWarning("[KeyController] 現在アクティブなオブジェクトにAnimatorがアタッチされていません。");
            }
        }

        // ----------------------------------------------------
        // 5. 手法 (提案手法 / 従来手法) の瞬時切り替え (Mキー) - Ablation Study
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                // 全ての提案機能のON/OFFを一括でトグルする
                bool isAnyOn = PCDRendererFeature.Instance.settings.enableTagBasedOptimization || 
                               PCDRendererFeature.Instance.settings.enableTypeAwareDensity || 
                               PCDRendererFeature.Instance.settings.enableSoftOcclusionFade || 
                               (PCDRendererFeature.Instance.settings.holeFillingMethod != PCDRendererFeature.PCV_HoleFillingMethod.None);

                bool toggleTo = !isAnyOn; // 1つでもONならすべてOFFにする

                PCDRendererFeature.Instance.settings.enableTagBasedOptimization = toggleTo;
                PCDRendererFeature.Instance.settings.enableTypeAwareDensity = toggleTo;
                PCDRendererFeature.Instance.settings.enableSoftOcclusionFade = toggleTo;
                PCDRendererFeature.Instance.settings.holeFillingMethod = toggleTo ? PCDRendererFeature.PCV_HoleFillingMethod.JointBilateral : PCDRendererFeature.PCV_HoleFillingMethod.None;

                string methodStr = toggleTo ? "提案手法 (全てON)" : "従来手法 (全てOFF)";
                Debug.Log($"[KeyController] 手法切り替え: {methodStr}");
            }
        }

        // ----------------------------------------------------
        // 6. 各提案機能(Ablation)の個別切り替え (Alpha1, 2, 3, 4)
        // ----------------------------------------------------
        if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                PCDRendererFeature.Instance.settings.enableTagBasedOptimization = !PCDRendererFeature.Instance.settings.enableTagBasedOptimization;
                Debug.Log($"[KeyController] ① タグスキップ最適化: {(PCDRendererFeature.Instance.settings.enableTagBasedOptimization ? "ON" : "OFF")}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                PCDRendererFeature.Instance.settings.enableTypeAwareDensity = !PCDRendererFeature.Instance.settings.enableTypeAwareDensity;
                Debug.Log($"[KeyController] ② 密度計算補正: {(PCDRendererFeature.Instance.settings.enableTypeAwareDensity ? "ON" : "OFF")}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                PCDRendererFeature.Instance.settings.enableSoftOcclusionFade = !PCDRendererFeature.Instance.settings.enableSoftOcclusionFade;
                Debug.Log($"[KeyController] ③ ソフトフェード: {(PCDRendererFeature.Instance.settings.enableSoftOcclusionFade ? "ON" : "OFF")}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                if (PCDRendererFeature.Instance.settings.holeFillingMethod == PCDRendererFeature.PCV_HoleFillingMethod.None)
                {
                    PCDRendererFeature.Instance.settings.holeFillingMethod = PCDRendererFeature.PCV_HoleFillingMethod.JointBilateral;
                }
                else if (PCDRendererFeature.Instance.settings.holeFillingMethod == PCDRendererFeature.PCV_HoleFillingMethod.JointBilateral)
                {
                    PCDRendererFeature.Instance.settings.holeFillingMethod = PCDRendererFeature.PCV_HoleFillingMethod.PullPush;
                }
                else if (PCDRendererFeature.Instance.settings.holeFillingMethod == PCDRendererFeature.PCV_HoleFillingMethod.PullPush)
                {
                    PCDRendererFeature.Instance.settings.holeFillingMethod = PCDRendererFeature.PCV_HoleFillingMethod.Morphology;
                }
                else
                {
                    PCDRendererFeature.Instance.settings.holeFillingMethod = PCDRendererFeature.PCV_HoleFillingMethod.None;
                }
                Debug.Log($"[KeyController] ④ 穴埋め(Hole Filling): {PCDRendererFeature.Instance.settings.holeFillingMethod}");
            }
        }

        // ----------------------------------------------------
        // 7. Fade Width設定切り替え (Tキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                if (PCDRendererFeature.Instance.settings.occlusionFadeWidth > 0.05f)
                {
                    PCDRendererFeature.Instance.settings.occlusionFadeWidth = 0.0f;
                    Debug.Log("[KeyController] FadeWidth: 0.0 (くっきりマスク)");
                }
                else
                {
                    PCDRendererFeature.Instance.settings.occlusionFadeWidth = 0.2f;
                    Debug.Log("[KeyController] FadeWidth: 0.2 (滑らかマスク)");
                }
            }
        }

        // ----------------------------------------------------
        // 8. カラーモードの切り替え (Cキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (materialController != null)
            {
                // Enumの値をローテーションさせる
                PointCloudColorMode nextMode = (PointCloudColorMode)(((int)materialController.colorMode + 1) % Enum.GetValues(typeof(PointCloudColorMode)).Length);
                materialController.ChangeColorMode(nextMode);
                Debug.Log($"[KeyController] カラーモード切り替え: {nextMode}");
            }
            else
            {
                Debug.LogWarning("[KeyController] materialControllerが設定されていません。");
            }
        }

        // ----------------------------------------------------
        // 7. 対象オブジェクト(狐など)の移動 (W,A,S,D / Q,E)
        // ----------------------------------------------------
        if (targetTransform != null)
        {
            Vector3 move = Vector3.zero;

            // X軸, Z軸移動: WASD または 十字キー
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;

            // Y軸移動: E (上) / Q (下)
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move += Vector3.down;

            if (move != Vector3.zero)
            {
                // カメラの向き等に関係なく、ワールド空間に対して自由に移動させる
                targetTransform.Translate(move.normalized * (moveSpeed * Time.deltaTime), Space.World);
            }
        }

        // ----------------------------------------------------
        // 9. PixelTag Map の切り替え (Pキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                PCDRendererFeature.Instance.settings.enablePixelTagMap = !PCDRendererFeature.Instance.settings.enablePixelTagMap;
                Debug.Log($"[KeyController] PixelTag Map: {(PCDRendererFeature.Instance.settings.enablePixelTagMap ? "ON" : "OFF")}");
            }
            else
            {
                Debug.LogWarning("[KeyController] PCDRendererFeature.Instance or settings is null; cannot toggle PixelTag Map.");
            }
        }

        // ----------------------------------------------------
        // 10. Occlusion Map の切り替え (Oキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                PCDRendererFeature.Instance.settings.enableOcclusionMap = !PCDRendererFeature.Instance.settings.enableOcclusionMap;
                Debug.Log($"[KeyController] Occlusion Map: {(PCDRendererFeature.Instance.settings.enableOcclusionMap ? "ON" : "OFF")}");
            }
            else
            {
                Debug.LogWarning("[KeyController] PCDRendererFeature.Instance or settings is null; cannot toggle Occlusion Map.");
            }
        }

        // ----------------------------------------------------
        // 11. Kernel Type の切り替え (Lキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                PCV_OcclusionKernel nextMode = (PCV_OcclusionKernel)(((int)PCDRendererFeature.Instance.settings.kernelType + 1) % Enum.GetValues(typeof(PCV_OcclusionKernel)).Length);
                PCDRendererFeature.Instance.settings.kernelType = nextMode;
                Debug.Log($"[KeyController] Kernel Type: {nextMode}");
            }
            else
            {
                Debug.LogWarning("[KeyController] PCDRendererFeature.Instance or settings is null; cannot toggle Kernel Type.");
            }
        }

        // ----------------------------------------------------
        // 12. Binning Method の切り替え (Kキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                PCV_OcclusionBinning nextMode = (PCV_OcclusionBinning)(((int)PCDRendererFeature.Instance.settings.binningMethod + 1) % Enum.GetValues(typeof(PCV_OcclusionBinning)).Length);
                PCDRendererFeature.Instance.settings.binningMethod = nextMode;
                Debug.Log($"[KeyController] Binning Method: {nextMode}");
            }
            else
            {
                Debug.LogWarning("[KeyController] PCDRendererFeature.Instance or settings is null; cannot toggle Binning Method.");
            }
        }

        // ----------------------------------------------------
        // 13. Direction Count の切り替え (Jキー)
        // ----------------------------------------------------
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.settings != null)
            {
                // Enumの値が不連続(1, 3, 6, 8)であるため、インデックスベースで循環させる
                Array values = Enum.GetValues(typeof(PCV_OcclusionDirectionCount));
                int currentIndex = Array.IndexOf(values, PCDRendererFeature.Instance.settings.directionCount);
                int nextIndex = (currentIndex + 1) % values.Length;
                PCV_OcclusionDirectionCount nextCount = (PCV_OcclusionDirectionCount)values.GetValue(nextIndex);

                PCDRendererFeature.Instance.settings.directionCount = nextCount;
                Debug.Log($"[KeyController] Direction Count: {nextCount}");
            }
            else
            {
                Debug.LogWarning("[KeyController] PCDRendererFeature.Instance or settings is null; cannot toggle Direction Count.");
            }
        }
    }
}
