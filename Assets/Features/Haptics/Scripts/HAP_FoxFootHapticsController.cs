using UnityEngine;
using System.Collections.Generic;
using System;

#if !USE_AUTD3_LEGACY
using AUTD3;
using AUTD3.Holo;
#else
using AUTD3Sharp;
using AUTD3Sharp.Gain.Holo;
using static AUTD3Sharp.Units;
#endif

#nullable enable

/// <summary>
/// Foxの足ハプティクスにおけるターゲット追跡・照射方式の指定。
/// </summary>
public enum FootHapticsTrackMode
{
    /// <summary>
    /// 接地しているすべての足へ同時に照射します（複数焦点）。
    /// </summary>
    Simultaneous,

    /// <summary>
    /// 接地している足を時分割で1本ずつ切り替えて照射します（単焦点）。
    /// </summary>
    Sequential
}

/// <summary>
/// Foxのボーン階層から4本の足の座標を特定し、
/// HAP_AUTDControllerの送信ループ（UpdateHaptics）内で足の位置にGSPAT等のホログラフィ触覚を照射するクラス。
/// </summary>
public class HAP_FoxFootHapticsController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("超音波を制御する HAP_AUTDController の参照。未指定の場合はシーン内から自動取得します。")]
    public HAP_AUTDController? autdController;

    [Header("Foot Bone Transforms")]
    [Tooltip("左前足のボーンTransform。")]
    public Transform? frontLeftFoot;
    [Tooltip("右前足のボーンTransform。")]
    public Transform? frontRightFoot;
    [Tooltip("左後足のボーンTransform。")]
    public Transform? backLeftFoot;
    [Tooltip("右後足のボーンTransform。")]
    public Transform? backRightFoot;

    [Header("Foot Toggles")]
    public bool enableFrontLeft = true;
    public bool enableFrontRight = true;
    public bool enableBackLeft = true;
    public bool enableBackRight = true;

    [Header("Animation State Settings")]
    [Tooltip("有効時、足が空中に浮いているとき（ジャンプ中など）は触覚をオフにします。")]
    public bool disableWhenInAir = false;
    [Tooltip("接地判定を行うための、ルート位置からの高さのしきい値（メートル）。")]
    public float airborneHeightThreshold = 0.05f;
    [Tooltip("接地の基準となるキャラクターのルートTransform。未指定の場合は本GameObjectのTransformを使用します。")]
    public Transform? rootTransform;

    [Header("Hand Contact Settings")]
    [Tooltip("有効時、HCD_Pipelineで検出された手の点群（クラスタ）が足の近くにある時のみ照射します。")]
    public bool onlyTargetHandContact = false;
    [Tooltip("手との接触と判定する距離のしきい値（メートル）。")]
    public float handContactThreshold = 0.1f;

    [Tooltip("どのAUTDデバイスが照射するかを、方向グルーピングで判定する際のクラスタ法線。\n上面から照射するAUTD：Vector3.downを指定（展開面が下向き，足に向かって上からめがける場合）。\n下面から照射するAUTD：Vector3.upを指定。")]
    public Vector3 footTargetNormal = Vector3.down;

    public enum FoxFootSTMMode
    {
        FociSTM,
        GainSTM
    }

    [Header("Custom Mode Settings")]
    [Tooltip("STMの種類を選択。FociSTM(ハードウェア計算・単焦点)、GainSTM(CPU計算・GSPAT等の複数焦点に対応)")]
    public FoxFootSTMMode stmMode = FoxFootSTMMode.FociSTM;

    [Tooltip("ハードウェアSTMを用いた高速シーケンシャル照射時の周波数（Hz）。")]
    public float sequentialSTMFrequency = 150f;

    [Tooltip("照射ターゲットの追跡・照射モード。\nSimultaneous: 接地した足をすべて同時に狙う（複数焦点）。\nSequential: 接地した足を1本ずつ順次切り替える（単焦点）。")]
    public FootHapticsTrackMode trackMode = FootHapticsTrackMode.Sequential;

    [Tooltip("単焦点計算に使用する内部ソルバー。\nNaive: 単焦点向けに最適で素子数にO(N)。\nGSPAT: 多焦点向けの反復最適化計算で負荷が高い。")]
    public HoloSolverAlgorithm customInnerAlgorithm = HoloSolverAlgorithm.Naive;

    [Header("Debug Visualization")]
    [Tooltip("Sceneビュー上に足の位置を示すGizmoを描画します。")]
    public bool drawGizmos = true;
    [Tooltip("照射対象となっている足のGizmo色。")]
    public Color activeColor = Color.green;
    [Tooltip("照射対象から外れている（非アクティブまたは非接地）足のGizmo色。")]
    public Color inactiveColor = Color.red;

    private void Reset()
    {
        autdController = FindAnyObjectByType<HAP_AUTDController>();
        rootTransform = this.transform;
        AutoDetectBones();
    }

    private void Awake()
    {
        if (autdController == null)
        {
            autdController = FindAnyObjectByType<HAP_AUTDController>();
        }

        if (rootTransform == null)
        {
            rootTransform = this.transform;
        }

        AutoDetectBones();
    }

    private void OnEnable()
    {
        if (autdController != null)
        {
            autdController.foxFootHapticsController = this;
        }
    }

    private void OnDisable()
    {
        if (autdController != null && autdController.foxFootHapticsController == this)
        {
            autdController.foxFootHapticsController = null;
        }
    }

    /// <summary>
    /// Foxの標準的なボーン階層名から、4本の足のTransformを自動検出してバインドします。
    /// </summary>
    public void AutoDetectBones()
    {
        Transform searchRoot = rootTransform != null ? rootTransform : this.transform;

        // Fox prefabのボーン名: Fox_F_LLegDigit11 / Fox_F_RLegDigit11 / Fox_LLegDigit11 / Fox_RLegDigit11 を対象とする
        if (frontLeftFoot == null)
            frontLeftFoot = FindChildRecursive(searchRoot, name => name.Contains("F_LLegDigit11") || name.Contains("Fox_F_LLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));
        
        if (frontRightFoot == null)
            frontRightFoot = FindChildRecursive(searchRoot, name => name.Contains("F_RLegDigit11") || name.Contains("Fox_F_RLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));

        if (backLeftFoot == null)
            backLeftFoot = FindChildRecursive(searchRoot, name => (name.Contains("LLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));

        if (backRightFoot == null)
            backRightFoot = FindChildRecursive(searchRoot, name => (name.Contains("RLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));

        // 検出できなかった場合のフォールバックとして Ankle を探す
        if (frontLeftFoot == null)
            frontLeftFoot = FindChildRecursive(searchRoot, name => name.Contains("F_LLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && name.ToLower().Contains("ankle")));
        if (frontRightFoot == null)
            frontRightFoot = FindChildRecursive(searchRoot, name => name.Contains("F_RLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && name.ToLower().Contains("ankle")));
        if (backLeftFoot == null)
            backLeftFoot = FindChildRecursive(searchRoot, name => (name.Contains("LLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));
        if (backRightFoot == null)
            backRightFoot = FindChildRecursive(searchRoot, name => (name.Contains("RLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));
    }

    private Transform? FindChildRecursive(Transform parent, Func<string, bool> predicate)
    {
        if (predicate(parent.name)) return parent;
        foreach (Transform child in parent)
        {
            var found = FindChildRecursive(child, predicate);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 現在有効かつ接地（しきい値内）している足が1つ以上あるかどうかを返します。
    /// </summary>
    public bool HasActiveTargets()
    {
        return IsFootActive(frontLeftFoot, enableFrontLeft) ||
               IsFootActive(frontRightFoot, enableFrontRight) ||
               IsFootActive(backLeftFoot, enableBackLeft) ||
               IsFootActive(backRightFoot, enableBackRight);
    }

    private bool IsFootActive(Transform? footTransform, bool isEnabled)
    {
        if (footTransform == null) return false;
        if (!isEnabled) return false;
        if (disableWhenInAir && rootTransform != null)
        {
            float relHeight = footTransform.position.y - rootTransform.position.y;
            if (relHeight > airborneHeightThreshold) return false;
        }

        if (onlyTargetHandContact)
        {
            bool hasContact = false;
            HCD_Pipeline? pipeline = (autdController != null) ? autdController.hcdPipeline : null;
            if (pipeline == null) pipeline = FindAnyObjectByType<HCD_Pipeline>();

            if (pipeline != null)
            {
                var clusters = pipeline.GetTrackedClusters();
                if (clusters != null)
                {
                    foreach (var c in clusters)
                    {
                        if (c.IsAlive && Vector3.Distance(c.Centroid, footTransform.position) <= handContactThreshold)
                        {
                            hasContact = true;
                            break;
                        }
                    }
                }
            }
            if (!hasContact) return false;
        }

        return true;
    }

    /// <summary>
    /// HAP_AUTDControllerが使用する、現在有効な足の座標データ（ClusterFociData）のリストを構築します。
    /// holoAlgorithm=Custom + customInnerAlgorithm=Naive のときは疑似STM（時計回り単焦点巻回）。
    /// holoAlgorithm=Custom + customInnerAlgorithm=GSPAT のときは接地足全てに同時マルチフォーカスGSPAT。
    /// </summary>
    public List<HAP_FociGenerator.ClusterFociData> GetFootFociList(float defaultIntensityPascal, Vector3 offset)
    {
        var result = new List<HAP_FociGenerator.ClusterFociData>();
        
        // FociSTM は強制的にハードウェアSTM(単焦点シーケンシャル)。
        // GainSTM は Sequential モードの時に PC計算STM(GSPAT等)のシーケンシャルになります。
        bool useCustomCycle = autdController != null 
            && autdController.holoAlgorithm == HoloAlgorithm.Custom
            && (stmMode == FoxFootSTMMode.FociSTM || (stmMode == FoxFootSTMMode.GainSTM && trackMode == FootHapticsTrackMode.Sequential));

        if (useCustomCycle)
        {
            // 時計回りの順に候補リストを作成: FL -> FR -> BR -> BL
            var candidates = new List<(Transform? transform, bool enabled)>
            {
                (frontLeftFoot, enableFrontLeft),
                (frontRightFoot, enableFrontRight),
                (backRightFoot, enableBackRight),
                (backLeftFoot, enableBackLeft)
            };

            // 有効かつ接地している候補を抽出
            var activeCandidates = new List<Transform>();
            foreach (var c in candidates)
            {
                if (c.transform != null && IsFootActive(c.transform, c.enabled))
                {
                    activeCandidates.Add(c.transform);
                }
            }

            if (activeCandidates.Count > 0)
            {
                // 代表点のダミークラスタを作成
                TrackedCluster dummyCluster = new TrackedCluster
                {
                    Centroid = activeCandidates[0].position,
                    Normal = footTargetNormal.normalized,
                    Force = 1.0f,
                    IsAlive = true
                };

                var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);
                fociData.UseSTM = true;
                fociData.IsGainSTM = (stmMode == FoxFootSTMMode.GainSTM);
                fociData.STMFrequency = sequentialSTMFrequency;

                foreach (var targetFoot in activeCandidates)
                {
                    Vector3 pos = targetFoot.position;
                    // 各足を個別のSTMフレームとして追加
                    fociData.STMFrames.Add(new List<Vector3> { 
                        new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z) 
                    });
                }
                
                result.Add(fociData);
            }
        }
        else
        {
            // GSPATマルチフォーカスモード: 接地判定を通過したすべての有効な足へ同時に照射

            ProcessFootFoci(frontLeftFoot, enableFrontLeft, defaultIntensityPascal, offset, result);
            ProcessFootFoci(frontRightFoot, enableFrontRight, defaultIntensityPascal, offset, result);
            ProcessFootFoci(backRightFoot, enableBackRight, defaultIntensityPascal, offset, result);
            ProcessFootFoci(backLeftFoot, enableBackLeft, defaultIntensityPascal, offset, result);
        }

        return result;
    }

    private void ProcessFootFoci(
        Transform? footTransform, 
        bool isEnabled, 
        float defaultIntensityPascal, 
        Vector3 offset, 
        List<HAP_FociGenerator.ClusterFociData> resultList)
    {
        if (!IsFootActive(footTransform, isEnabled) || footTransform == null) return;

        Vector3 pos = footTransform.position;

        // ダミーのTrackedClusterを構築して渡す
        // footTargetNormalは、どのAUTDデバイスを担当させるかの方向ヒント。
        // 上から照射するAUTDの場合: Normal = Vector3.down (デバイスの照射面が下向き)
        TrackedCluster dummyCluster = new TrackedCluster
        {
            Centroid = pos,
            Normal = footTargetNormal.normalized,
            Force = 1.0f,
            IsAlive = true
        };

        Debug.Log($"[FoxFoot] Target: {footTransform!.name} pos={pos} normal={footTargetNormal}");

        var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);

#if !USE_AUTD3_LEGACY
        fociData.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
            new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
            Amplitude.FromPascal(defaultIntensityPascal)
        ));
#else
        fociData.SequentialFoci.Add((
            new AUTD3Sharp.Utils.Point3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
            defaultIntensityPascal * Pa
        ));
#endif

        resultList.Add(fociData);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Transform searchRoot = rootTransform != null ? rootTransform : this.transform;

        // エディタ上でのプレビュー表示用に、メンバがnullなら一時的に取得する
        Transform? fl = frontLeftFoot;
        Transform? fr = frontRightFoot;
        Transform? bl = backLeftFoot;
        Transform? br = backRightFoot;

        if (fl == null || fr == null || bl == null || br == null)
        {
            fl = FindChildRecursive(searchRoot, name => name.Contains("F_LLegDigit11") || name.Contains("Fox_F_LLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));
            fr = FindChildRecursive(searchRoot, name => name.Contains("F_RLegDigit11") || name.Contains("Fox_F_RLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));
            bl = FindChildRecursive(searchRoot, name => (name.Contains("LLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));
            br = FindChildRecursive(searchRoot, name => (name.Contains("RLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));
        }

        DrawFootGizmo(fl, enableFrontLeft);
        DrawFootGizmo(fr, enableFrontRight);
        DrawFootGizmo(bl, enableBackLeft);
        DrawFootGizmo(br, enableBackRight);
    }

    private void DrawFootGizmo(Transform? footTransform, bool isEnabled)
    {
        if (footTransform == null) return;

        Vector3 pos = footTransform.position;
        bool active = isEnabled;
        bool isGrounded = true;

        bool useCustomCycle = autdController != null 
            && autdController.holoAlgorithm == HoloAlgorithm.Custom
            && trackMode == FootHapticsTrackMode.Sequential;
        // STMを用いる場合はアクティブな足すべてが高速で切り替わるため、すべてを有効として表示する
        // （特定の一つだけを光らせる処理は不要）

        if (disableWhenInAir && rootTransform != null)
        {
            float relHeight = pos.y - rootTransform.position.y;
            if (relHeight > airborneHeightThreshold)
            {
                isGrounded = false;
                active = false;
            }
        }

        if (onlyTargetHandContact)
        {
            bool hasContact = false;
            HCD_Pipeline? pipeline = (autdController != null) ? autdController.hcdPipeline : null;
            if (pipeline == null) pipeline = FindAnyObjectByType<HCD_Pipeline>();

            if (pipeline != null)
            {
                var clusters = pipeline.GetTrackedClusters();
                if (clusters != null)
                {
                    foreach (var c in clusters)
                    {
                        if (c.IsAlive && Vector3.Distance(c.Centroid, pos) <= handContactThreshold)
                        {
                            hasContact = true;
                            break;
                        }
                    }
                }
            }
            if (!hasContact)
            {
                active = false;
            }
        }

        // ベースカラーは「その足が機能として有効か（isEnabled）」で決める
        Color baseColor = isEnabled ? activeColor : inactiveColor;

        // 1. 接地判定（高さ判定）の可視化線を描画
        if (disableWhenInAir && rootTransform != null)
        {
            Vector3 groundPt = new Vector3(pos.x, rootTransform.position.y, pos.z);
            Vector3 threshPt = new Vector3(pos.x, rootTransform.position.y + airborneHeightThreshold, pos.z);

            // 許容高さしきい値を示す小さな十字を描画
            Gizmos.color = baseColor;
            Gizmos.DrawLine(threshPt - Vector3.left * 0.01f, threshPt + Vector3.left * 0.01f);
            Gizmos.DrawLine(threshPt - Vector3.forward * 0.01f, threshPt + Vector3.forward * 0.01f);

            if (isGrounded)
            {
                // 接地内: 足から地面までを有効色で結ぶ
                Gizmos.color = baseColor;
                Gizmos.DrawLine(pos, groundPt);
            }
            else
            {
                // 接地外: 地面からしきい値までは有効範囲のライン
                Gizmos.color = baseColor;
                Gizmos.DrawLine(threshPt, groundPt);

                // しきい値から足の位置（はみ出ている部分）は無効ライン（赤）で結ぶ
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, threshPt);
            }
        }

        // 手の接触判定の可視化 (誤解を招く大きな球は描画せず、接触時にクラスタへ線を引く)
        if (onlyTargetHandContact)
        {
            HCD_Pipeline? pipeline = (autdController != null) ? autdController.hcdPipeline : null;
            if (pipeline == null) pipeline = FindAnyObjectByType<HCD_Pipeline>();

            if (pipeline != null)
            {
                var clusters = pipeline.GetTrackedClusters();
                if (clusters != null)
                {
                    foreach (var c in clusters)
                    {
                        if (c.IsAlive && Vector3.Distance(c.Centroid, pos) <= handContactThreshold)
                        {
                            // 接触しているクラスタから足元へ線を引く
                            Gizmos.color = activeColor;
                            Gizmos.DrawLine(pos, c.Centroid);
                            Gizmos.DrawWireSphere(c.Centroid, 0.01f);
                            break;
                        }
                    }
                }
            }
        }

        // 2. 照射ターゲットの描画
        if (active)
        {
            // 照射中（有効なアクティブ状態）の場合は実線球を描画して強調
            Gizmos.color = activeColor;
            Gizmos.DrawSphere(pos, 0.01f);
        }
        else
        {
            // 無効な場合は位置を示すためのワイヤースフィアのみ描画
            Gizmos.color = baseColor;
            Gizmos.DrawWireSphere(pos, 0.01f);
        }
    }
}
