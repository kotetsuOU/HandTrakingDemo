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
/// Foxのボーン階層から4本の足と尻尾の座標を特定し、
/// HAP_AUTDControllerの送信ループ（UpdateHaptics）内で位置にGSPAT等のホログラフィ触覚を照射するクラス。
/// </summary>
public class HAP_FoxFootHapticsController : HAP_BaseObjectHapticsController
{
    [Header("Foot Bone Transforms")]
    [Tooltip("左前足のボーンTransform。")]
    public Transform? frontLeftFoot;
    [Tooltip("右前足のボーンTransform。")]
    public Transform? frontRightFoot;
    [Tooltip("左後足のボーンTransform。")]
    public Transform? backLeftFoot;
    [Tooltip("右後足のボーンTransform。")]
    public Transform? backRightFoot;
    [Tooltip("尻尾のボーンTransform。")]
    public Transform? tailBone;

    [Header("Foot Toggles")]
    public bool enableFrontLeft = true;
    public bool enableFrontRight = true;
    public bool enableBackLeft = true;
    public bool enableBackRight = true;
    public bool enableTail = true;

    /// <summary>
    /// 各部位のターゲット情報をリストにして返します（基底クラスのGizmo描画や判定に利用）。
    /// </summary>
    public override List<HapticsTargetInfo> TargetInfos
    {
        get
        {
            var list = new List<HapticsTargetInfo>();
            if (frontLeftFoot != null) list.Add(new HapticsTargetInfo { Name = "Front Left", Transform = frontLeftFoot, IsEnabled = enableFrontLeft, IsTail = false });
            if (frontRightFoot != null) list.Add(new HapticsTargetInfo { Name = "Front Right", Transform = frontRightFoot, IsEnabled = enableFrontRight, IsTail = false });
            if (backLeftFoot != null) list.Add(new HapticsTargetInfo { Name = "Back Left", Transform = backLeftFoot, IsEnabled = enableBackLeft, IsTail = false });
            if (backRightFoot != null) list.Add(new HapticsTargetInfo { Name = "Back Right", Transform = backRightFoot, IsEnabled = enableBackRight, IsTail = false });
            if (tailBone != null) list.Add(new HapticsTargetInfo { Name = "Tail", Transform = tailBone, IsEnabled = enableTail, IsTail = true });
            return list;
        }
    }

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
            autdController.objectHapticsController = this;
        }
    }

    private void OnDisable()
    {
        if (autdController != null && autdController.objectHapticsController == this)
        {
            autdController.objectHapticsController = null;
        }
    }

    /// <summary>
    /// Foxの標準的なボーン階層名から、4本の足および尻尾のTransformを自動検出してバインドします。
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

        // 尻尾の自動検出（先端のTail6から探し、なければTail5、それでもなければTailを含むものを探す）
        if (tailBone == null)
            tailBone = FindChildRecursive(searchRoot, name => name.Contains("Tail6") || name.Contains("Fox_Tail6"));
        if (tailBone == null)
            tailBone = FindChildRecursive(searchRoot, name => name.Contains("Tail5") || name.Contains("Fox_Tail5"));
        if (tailBone == null)
            tailBone = FindChildRecursive(searchRoot, name => name.ToLower().Contains("tail"));

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
    /// HAP_AUTDControllerが使用する、現在有効なターゲット（足・尻尾）の座標データ（ClusterFociData）のリストを構築します。
    /// holoAlgorithm=Custom + customInnerAlgorithm=Naive のときは疑似STM（時計回り単焦点巻回）。
    /// holoAlgorithm=Custom + customInnerAlgorithm=GSPAT のときは接地足全てに同時マルチフォーカスGSPAT。
    /// </summary>
    public override List<HAP_FociGenerator.ClusterFociData> GetHapticsTargets(float defaultIntensityPascal, Vector3 offset)
    {
        var result = new List<HAP_FociGenerator.ClusterFociData>();
        
        bool useCustomCycle = autdController != null 
            && autdController.holoAlgorithm == HoloAlgorithm.Custom
            && (stmMode == HapticsSTMMode.FociSTM || (stmMode == HapticsSTMMode.GainSTM && trackMode == HapticsTrackMode.Sequential));

        if (useCustomCycle)
        {
            var candidates = new List<(Transform? transform, bool enabled, bool isTail)>
            {
                (frontLeftFoot, enableFrontLeft, false),
                (frontRightFoot, enableFrontRight, false),
                (backRightFoot, enableBackRight, false),
                (backLeftFoot, enableBackLeft, false),
                (tailBone, enableTail, true)
            };

            var activeCandidates = new List<Transform>();
            foreach (var c in candidates)
            {
                bool isActive = IsTargetActive(c.transform, c.enabled, c.isTail);
                if (c.transform != null && isActive)
                {
                    activeCandidates.Add(c.transform);
                }
            }

            if (activeCandidates.Count > 0)
            {
                TrackedCluster dummyCluster = new TrackedCluster
                {
                    Centroid = activeCandidates[0].position,
                    Normal = footTargetNormal.normalized,
                    Force = 1.0f,
                    IsAlive = true
                };

                var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);
                fociData.UseSTM = true;
                fociData.IsGainSTM = (stmMode == HapticsSTMMode.GainSTM);
                fociData.STMFrequency = sequentialSTMFrequency;

                foreach (var targetFoot in activeCandidates)
                {
                    Vector3 pos = targetFoot.position;
                    fociData.STMFrames.Add(new List<Vector3> { 
                        new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z) 
                    });
                }
                
                result.Add(fociData);
            }
        }
        else
        {
            ProcessTargetFoci(frontLeftFoot, enableFrontLeft, false, defaultIntensityPascal, offset, result);
            ProcessTargetFoci(frontRightFoot, enableFrontRight, false, defaultIntensityPascal, offset, result);
            ProcessTargetFoci(backRightFoot, enableBackRight, false, defaultIntensityPascal, offset, result);
            ProcessTargetFoci(backLeftFoot, enableBackLeft, false, defaultIntensityPascal, offset, result);
            ProcessTargetFoci(tailBone, enableTail, true, defaultIntensityPascal, offset, result);
        }

        return result;
    }

    private void ProcessTargetFoci(
        Transform? targetTransform, 
        bool isEnabled, 
        bool isTail,
        float defaultIntensityPascal, 
        Vector3 offset, 
        List<HAP_FociGenerator.ClusterFociData> resultList)
    {
        if (targetTransform == null) return;
        bool isActive = IsTargetActive(targetTransform, isEnabled, isTail);
        if (!isActive) return;

        Vector3 pos = targetTransform.position;

        TrackedCluster dummyCluster = new TrackedCluster
        {
            Centroid = pos,
            Normal = footTargetNormal.normalized,
            Force = 1.0f,
            IsAlive = true
        };

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
}
