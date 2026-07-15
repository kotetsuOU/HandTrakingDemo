#if USE_AUTD3_LEGACY

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static AUTD3Sharp.Units;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDController autdController = null!;

    [Header("Calibration Mode")]
    [Tooltip("譛牙柑蛹悶☆繧九→騾壼ｸｸ縺ｮHaptics蜃ｺ蜉帙ｒ繝舌う繝代せ縺励√％縺ｮ險ｭ螳壹↓蝓ｺ縺･縺・◆繝・せ繝亥・蜉帙ｒ陦後＞縺ｾ縺吶・)]
    public bool enableCalibration = false;

    [Header("Target Devices")]
    [Tooltip("蜃ｺ蜉帛ｯｾ雎｡縺ｨ縺吶ｋAUTD繝・ヰ繧､繧ｹ縺ｮ繧､繝ｳ繝・ャ繧ｯ繧ｹ")]
    public List<bool> targetDevices = new List<bool>();

    [Header("Focus Settings")]
    public bool useMultiFocus = false;
    [Tooltip("謖・ｮ壹＆繧後※縺・ｋ蝣ｴ蜷医・縺薙・Transform縺ｮ菴咲ｽｮ繧貞腰辟ｦ轤ｹ縺ｨ縺励※菴ｿ逕ｨ縺励∪縺・)]
    public Transform? singleFocusTarget;
    public Vector3 singleFocusPosition = Vector3.zero;
    public List<Vector3> multiFocusPositions = new List<Vector3> { Vector3.zero };
    
    [Tooltip("繧ｭ繝｣繝ｪ繝悶Ξ繝ｼ繧ｷ繝ｧ繝ｳ譎ゅ・豁｣隗｣菴咲ｽｮ・亥ｮ滄圀縺ｫ辟ｦ轤ｹ縺悟粋縺｣縺ｦ縺・ｋ縺ｹ縺咲黄逅・噪縺ｪ菴咲ｽｮ・・)]
    public Transform? truePositionTarget;
    
    [Range(0f, 1f)]
    public float focusAmplitude = 1f;

    void Update()
    {
        if (autdController == null) return;

        // 繧ｭ繝｣繝ｪ繝悶Ξ繝ｼ繧ｷ繝ｧ繝ｳ縺梧怏蜉ｹ縺ｪ蝣ｴ蜷医・繧ｳ繝ｳ繝医Ο繝ｼ繝ｩ繝ｼ縺ｮ閾ｪ蜍募・蜉帙ｒ繝舌う繝代せ
        autdController.bypassHaptics = enableCalibration;

        if (enableCalibration)
        {
            EmitCalibrationFocus();
        }
    }

    private void EmitCalibrationFocus()
    {
        // 繧ｿ繝ｼ繧ｲ繝・ヨ繝・ヰ繧､繧ｹ縺梧欠螳壹＆繧後※縺・↑縺・ｴ蜷医・菴輔ｂ縺励↑縺・
        if (targetDevices == null || targetDevices.Count == 0) return;

        bool allTrue = targetDevices.Count > 0 && targetDevices.Count == autdController.connectedDevices.Count;
        foreach (var b in targetDevices) if (!b) allTrue = false;

        byte intensityVal = (byte)Mathf.Clamp(focusAmplitude * 255f, 0f, 255f);

        object targetDatagram;
        
        if (useMultiFocus && multiFocusPositions.Count > 0)
        {
            var activeFoci = new (AUTD3Sharp.Utils.Point3, AUTD3Sharp.Gain.Holo.Amplitude)[multiFocusPositions.Count];
            for (int i = 0; i < multiFocusPositions.Count; i++)
            {
                var p = multiFocusPositions[i];
                activeFoci[i] = (
                    new AUTD3Sharp.Utils.Point3(p.x + autdController.offset.x, p.y + autdController.offset.y, p.z + autdController.offset.z),
                    focusAmplitude * 10000f * Pa // 繧ｭ繝｣繝ｪ繝悶Ξ繝ｼ繧ｷ繝ｧ繝ｳ逕ｨ縺ｯ邁｡譏鍋噪縺ｫ譛螟ｧ10000Pa縺ｫ繧ｹ繧ｱ繝ｼ繝ｫ
                );
            }
            targetDatagram = null(activeFoci, null);
        }
        else
        {
            Vector3 pos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
            var p = new AUTD3Sharp.Utils.Point3(
                pos.x + autdController.offset.x, 
                pos.y + autdController.offset.y, 
                pos.z + autdController.offset.z
            );
            targetDatagram = null;
        }

        // 繝・ヰ繝・げ辟｡蜉ｹ蛹悶′蟄伜惠縺吶ｋ縺九∽ｸ驛ｨ縺ｮ繝・ヰ繧､繧ｹ縺ｮ縺ｿ蜃ｺ蜉帙☆繧句ｴ蜷医・蛟句挨縺ｫ繧ｰ繝ｫ繝ｼ繝励Ν繝ｼ繝・ぅ繝ｳ繧ｰ縺吶ｋ
        bool hasDisabledDevice = autdController.debugDisabler != null && autdController.connectedDevices.Any(d => autdController.debugDisabler.IsDisabled(d.ID));
        
        if (allTrue && !hasDisabledDevice)
        {
            autdController.Send(targetDatagram);
        }
        else
        {
            var groupDict = new object();
            groupDict.Add("target", targetDatagram);
            groupDict.Add("null", null);

            // 繝・ヰ繧､繧ｹ繧､繝ｳ繝・ャ繧ｯ繧ｹ縺ｫ蠢懊§縺ｦ蜃ｺ蜉帙ｒ蛻・ｊ譖ｿ縺・
            string[] mapping = new string[autdController.connectedDevices.Count];
            for (int i = 0; i < autdController.connectedDevices.Count; i++) {
                if (autdController.connectedDevices[i] == null) {
                    mapping[i] = "null";
                    continue;
                }
                var deviceId = autdController.connectedDevices[i].ID;
                if (autdController.debugDisabler != null && autdController.debugDisabler.IsDisabled(deviceId)) {
                    mapping[i] = "null";
                } else if (i < targetDevices.Count && targetDevices[i]) {
                    mapping[i] = "target";
                } else {
                    mapping[i] = "null";
                }
            }

            autdController.SetGainGroup(dev => 
            {
                int deviceIndex = dev.Idx();
                if (deviceIndex < 0 || deviceIndex >= mapping.Length) return "null";
                return mapping[deviceIndex];
            }, groupDict);
        }
    }

    /// <summary>
    /// 迴ｾ蝨ｨ縺ｮ縺薙・繧ｪ繝悶ず繧ｧ繧ｯ繝医・Transform繧但UTDController縺ｮOffset縺ｫ驕ｩ逕ｨ縺励∽ｽ咲ｽｮ繧偵Μ繧ｻ繝・ヨ縺励∪縺・
    /// </summary>
    public void ApplyOffset() { syntaxError }
    {
        if (autdController == null) return;
        
        autdController.offset += this.transform.localPosition;
        this.transform.localPosition = Vector3.zero;
        this.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 迴ｾ蝨ｨ縺ｮFocusTarget縺ｨ豁｣隗｣菴咲ｽｮ・・ruePositionTarget・峨・蟾ｮ蛻・°繧峨が繝輔そ繝・ヨ繧定ｨ育ｮ励＠驕ｩ逕ｨ縺励∪縺・
    /// </summary>
    public void ApplyOffsetByDifference()
    {
        if (autdController == null) return;
        
        Vector3 focusPos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
        
        if (truePositionTarget == null)
        {
            Debug.LogWarning("[Calibration] truePositionTarget is not set. Cannot apply difference.");
            return;
        }

        Vector3 diff = focusPos - truePositionTarget.position;
        autdController.offset += diff;
        
        Debug.Log($"[Calibration] Applied offset by difference: {diff}. New Offset: {autdController.offset}");
    }

    /// <summary>
    /// 迴ｾ蝨ｨ縺ｮoffset繧探argetDevices縺ｧ驕ｸ謚槭＆繧後※縺・ｋAUTD3Device縺ｮTransform縺ｫ豌ｸ邯夂噪縺ｫ蜿肴丐・・ake・峨＠縲｛ffset繧偵Μ繧ｻ繝・ヨ縺励∪縺吶・
    /// ・・argetPos_cmd = TargetPos + offset 縺後ョ繝舌う繧ｹ縺九ｉ縺ｮ逶ｸ蟇ｾ霍晞屬縺ｨ縺ｪ繧九◆繧√√ョ繝舌う繧ｹ閾ｪ菴薙ｒ -offset 遘ｻ蜍輔＆縺帙ｋ縺薙→縺ｧ蜷後§蜉ｹ譫懊ｒ蠕励∪縺呻ｼ・
    /// </summary>
    public void BakeOffsetToDevices()
    {
        if (autdController == null) return;

        Vector3 currentOffset = autdController.offset;
        if (currentOffset == Vector3.zero)
        {
            Debug.Log("[Calibration] Offset is already zero. Nothing to bake.");
            return;
        }

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None).OrderBy(d => d.ID).ToArray();
        if (devices.Length == 0)
        {
            Debug.LogWarning("[Calibration] No AUTD3Device found in the scene to bake to.");
            return;
        }

        int bakedCount = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            if (i < targetDevices.Count && targetDevices[i])
            {
                // Edit繝｢繝ｼ繝峨↑縺ｩ縺ｧUndo繧堤匳骭ｲ縺吶ｋ蝣ｴ蜷医・Editor繧ｹ繧ｯ繝ｪ繝励ヨ蛛ｴ縺ｧ陦後≧
                devices[i].transform.position -= currentOffset;
                bakedCount++;
            }
        }

        autdController.offset = Vector3.zero;
        Debug.Log($"[Calibration] Baked offset {currentOffset} to {bakedCount} selected devices. (Device positions moved by {-currentOffset}). Offset reset to zero.");
    }
}



#else

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDController autdController = null!;

    [Header("Calibration Mode")]
    [Tooltip("有効化すると通常のHaptics出力をバイパスし、この設定に基づぁEチEチE力を行います、E")]
    public bool enableCalibration = false;

    [Header("Target Devices")]
    [Tooltip("出力対象とするAUTDチEイスのインチEクス")]
    public List<bool> targetDevices = new List<bool>();

    [Header("Focus Settings")]
    [Range(0f, 1f)]
    public float focusAmplitude = 1.0f;
    
    [Space(10)]
    public bool useMultiFocus = false;

    [Header("Single Focus")]
    public Vector3 singleFocusPosition = new Vector3(0, 150f, 0);
    public Transform? singleFocusTarget;

    [Header("Multi Focus")]
    public List<Vector3> multiFocusPositions = new List<Vector3>();

    void Update()
    {
        if (autdController == null) return;
        autdController.bypassHaptics = enableCalibration;
        if (enableCalibration)
        {
            EmitCalibrationFocus();
        }
    }

    private void EmitCalibrationFocus()
    {
        Debug.LogWarning("HAP_AUTDCalibration is not yet fully implemented for v31");
    }

    public void ApplyOffset()
    {
        Debug.LogWarning("ApplyOffset is not yet fully implemented for v31");
    }

    public void ApplyOffsetByDifference()
    {
        Debug.LogWarning("ApplyOffsetByDifference is not yet fully implemented for v31");
    }

    public void BakeOffsetToDevices()
    {
        Debug.LogWarning("BakeOffsetToDevices is not yet fully implemented for v31");
    }
}

#endif

