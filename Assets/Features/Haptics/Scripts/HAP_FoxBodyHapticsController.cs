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
/// Foxのボーン階層から頭、耳、4本の足、尻尾の座標を特定し、
/// HAP_AUTDControllerの送信ループ（UpdateHaptics）内で各位置および照射向きにホログラフィ触覚を照射するクラス。
/// </summary>
public class HAP_FoxBodyHapticsController : HAP_BaseObjectHapticsController
{
    [Header("Body Bone Transforms")]
    [Tooltip("頭部のボーンTransform。")]
    public Transform? headBone;
    [Tooltip("左耳のボーンTransform。")]
    public Transform? leftEarBone;
    [Tooltip("右耳のボーンTransform。")]
    public Transform? rightEarBone;
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

    [Header("Body Part Toggles")]
    public bool enableHead = true;
    public bool enableLeftEar = true;
    public bool enableRightEar = true;
    public bool enableFrontLeft = true;
    public bool enableFrontRight = true;
    public bool enableBackLeft = true;
    public bool enableBackRight = true;
    public bool enableTail = true;

    [Header("Target Normals (照射向き)")]
    [Tooltip("頭部および耳ターゲット用の照射法線方向（デフォルト: Vector3.down）。")]
    public Vector3 headTargetNormal = Vector3.down;
    // 注: 足・尻尾用の照射法線方向は基底クラスの footTargetNormal (Vector3.down) を使用します。

    /// <summary>
    /// 各部位のターゲット情報をリストにして返します（基底クラスのGizmo描画や判定に利用）。
    /// 頭 -> 左耳 -> 右耳 -> 前左 -> 前右 -> 後右 -> 後左 -> 尻尾 の順で定義します。
    /// ※ 頭・耳・尻尾は接地判定 (disableWhenInAir) を適用しない特殊部位 (IsTail = true) として登録します。
    /// </summary>
    public override List<HapticsTargetInfo> TargetInfos
    {
        get
        {
            var list = new List<HapticsTargetInfo>();

            if (headBone != null)
                list.Add(new HapticsTargetInfo { Name = "Head", Transform = headBone, IsEnabled = enableHead, IsTail = true, Offset = Vector3.zero, Normal = headTargetNormal });

            if (leftEarBone != null)
                list.Add(new HapticsTargetInfo { Name = "Left Ear", Transform = leftEarBone, IsEnabled = enableLeftEar, IsTail = true, Offset = Vector3.zero, Normal = headTargetNormal });

            if (rightEarBone != null)
                list.Add(new HapticsTargetInfo { Name = "Right Ear", Transform = rightEarBone, IsEnabled = enableRightEar, IsTail = true, Offset = Vector3.zero, Normal = headTargetNormal });

            if (frontLeftFoot != null)
                list.Add(new HapticsTargetInfo { Name = "Front Left", Transform = frontLeftFoot, IsEnabled = enableFrontLeft, IsTail = false, Offset = Vector3.zero, Normal = footTargetNormal });

            if (frontRightFoot != null)
                list.Add(new HapticsTargetInfo { Name = "Front Right", Transform = frontRightFoot, IsEnabled = enableFrontRight, IsTail = false, Offset = Vector3.zero, Normal = footTargetNormal });

            if (backRightFoot != null)
                list.Add(new HapticsTargetInfo { Name = "Back Right", Transform = backRightFoot, IsEnabled = enableBackRight, IsTail = false, Offset = Vector3.zero, Normal = footTargetNormal });

            if (backLeftFoot != null)
                list.Add(new HapticsTargetInfo { Name = "Back Left", Transform = backLeftFoot, IsEnabled = enableBackLeft, IsTail = false, Offset = Vector3.zero, Normal = footTargetNormal });

            if (tailBone != null)
                list.Add(new HapticsTargetInfo { Name = "Tail", Transform = tailBone, IsEnabled = enableTail, IsTail = true, Offset = Vector3.zero, Normal = footTargetNormal });

            return list;
        }
    }

    private void Reset()
    {
        autdController = FindAnyObjectByType<HAP_AUTDHapticsController>();
        rootTransform = this.transform;
        AutoDetectBones();
    }

    private void Awake()
    {
        if (autdController == null)
        {
            autdController = FindAnyObjectByType<HAP_AUTDHapticsController>();
        }

        if (rootTransform == null)
        {
            rootTransform = this.transform;
        }

        AutoDetectBones();
    }

    protected virtual void OnEnable()
    {
        RegisterSelfToController();
    }

    protected virtual void OnDisable()
    {
        // 他のコントローラーに影響を与えずに安全に非アクティブ化
    }

    public void RegisterSelfToController()
    {
        if (autdController == null)
        {
            autdController = FindAnyObjectByType<HAP_AUTDHapticsController>();
        }

        if (autdController != null)
        {
            if (!autdController.objectHapticsControllers.Contains(this))
            {
                autdController.objectHapticsControllers.Add(this);
            }
        }
    }

    /// <summary>
    /// Foxの標準的なボーン階層名から、頭、耳、4本の足、および尻尾のTransformを自動検出してバインドします。
    /// </summary>
    public virtual void AutoDetectBones(bool forceOverwrite = false)
    {
        Transform searchRoot = rootTransform != null ? rootTransform : this.transform.root;
        if (searchRoot == null) searchRoot = this.transform;

        if (forceOverwrite)
        {
            headBone = null;
            leftEarBone = null;
            rightEarBone = null;
            frontLeftFoot = null;
            frontRightFoot = null;
            backLeftFoot = null;
            backRightFoot = null;
            tailBone = null;
        }

        // 頭部の自動検出 (Fox_Head / Head 等)
        if (headBone == null)
            headBone = FindChildRecursive(searchRoot, name => name.Equals("Fox_Head", StringComparison.OrdinalIgnoreCase) || name.Equals("Head", StringComparison.OrdinalIgnoreCase) || (name.ToLower().Contains("head") && !name.ToLower().Contains("overhead")));

        // 耳の自動検出 (Fox_LEar1 / Fox_REar1 を最優先とし、Fox_LEar2 / LEar1 / Ear_L 等に対応)
        if (leftEarBone == null)
            leftEarBone = FindChildRecursive(searchRoot, name => name.Equals("Fox_LEar1", StringComparison.OrdinalIgnoreCase) || name.Equals("Fox_LEar2", StringComparison.OrdinalIgnoreCase) || name.Contains("LEar1") || name.Contains("Ear1_L") || (name.ToLower().Contains("ear") && (name.ToLower().Contains("left") || name.ToLower().EndsWith("_l") || name.ToLower().Contains("_l_"))));

        if (rightEarBone == null)
            rightEarBone = FindChildRecursive(searchRoot, name => name.Equals("Fox_REar1", StringComparison.OrdinalIgnoreCase) || name.Equals("Fox_REar2", StringComparison.OrdinalIgnoreCase) || name.Contains("REar1") || name.Contains("Ear1_R") || (name.ToLower().Contains("ear") && (name.ToLower().Contains("right") || name.ToLower().EndsWith("_r") || name.ToLower().Contains("_r_"))));

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

        // 検出できなかった場合のフォールバックとして Ankle や Neck を探す
        if (headBone == null)
            headBone = FindChildRecursive(searchRoot, name => name.Contains("Fox_Neck") || name.ToLower().Contains("neck"));
        if (frontLeftFoot == null)
            frontLeftFoot = FindChildRecursive(searchRoot, name => name.Contains("F_LLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && name.ToLower().Contains("ankle")));
        if (frontRightFoot == null)
            frontRightFoot = FindChildRecursive(searchRoot, name => name.Contains("F_RLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && name.ToLower().Contains("ankle")));
        if (backLeftFoot == null)
            backLeftFoot = FindChildRecursive(searchRoot, name => (name.Contains("LLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));
        if (backRightFoot == null)
            backRightFoot = FindChildRecursive(searchRoot, name => (name.Contains("RLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));

        // それでも見つからない場合、親階層や別ルートの全検索
        if (headBone == null || leftEarBone == null || rightEarBone == null || frontLeftFoot == null || frontRightFoot == null || backLeftFoot == null || backRightFoot == null || tailBone == null)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                string n = t.name;
                if (headBone == null && (n.Contains("Head") || n.Contains("Fox_Head"))) headBone = t;
                if (leftEarBone == null && (n.Contains("Fox_LEar") || n.Contains("LEar1") || n.Contains("Ear_L"))) leftEarBone = t;
                if (rightEarBone == null && (n.Contains("Fox_REar") || n.Contains("REar1") || n.Contains("Ear_R"))) rightEarBone = t;
                if (frontLeftFoot == null && (n.Contains("F_LLegDigit") || (n.ToLower().Contains("front") && n.ToLower().Contains("left") && n.ToLower().Contains("digit")))) frontLeftFoot = t;
                if (frontRightFoot == null && (n.Contains("F_RLegDigit") || (n.ToLower().Contains("front") && n.ToLower().Contains("right") && n.ToLower().Contains("digit")))) frontRightFoot = t;
                if (backLeftFoot == null && (n.Contains("LLegDigit") && !n.Contains("F_"))) backLeftFoot = t;
                if (backRightFoot == null && (n.Contains("RLegDigit") && !n.Contains("F_"))) backRightFoot = t;
                if (tailBone == null && (n.Contains("Tail6") || n.Contains("Tail5"))) tailBone = t;
            }
        }
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
}
