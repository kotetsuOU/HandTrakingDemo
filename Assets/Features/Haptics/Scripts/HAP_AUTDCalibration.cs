using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Driver.Datagram;
using static AUTD3Sharp.Units;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDController autdController = null!;

    [Header("Calibration Mode")]
    [Tooltip("有効化すると通常のHaptics出力をバイパスし、この設定に基づいたテスト出力を行います。")]
    public bool enableCalibration = false;

    [Header("Target Devices")]
    [Tooltip("出力対象とするAUTDデバイスのインデックス")]
    public List<bool> targetDevices = new List<bool>();

    [Header("Focus Settings")]
    public bool useMultiFocus = false;
    [Tooltip("指定されている場合はこのTransformの位置を単焦点として使用します")]
    public Transform singleFocusTarget;
    public Vector3 singleFocusPosition = Vector3.zero;
    public List<Vector3> multiFocusPositions = new List<Vector3> { Vector3.zero };
    
    [Tooltip("キャリブレーション時の正解位置（実際に焦点が合っているべき物理的な位置）")]
    public Transform truePositionTarget;
    
    [Range(0f, 1f)]
    public float focusAmplitude = 1f;

    void Update()
    {
        if (autdController == null) return;

        // キャリブレーションが有効な場合はコントローラーの自動出力をバイパス
        autdController.bypassHaptics = enableCalibration;

        if (enableCalibration)
        {
            EmitCalibrationFocus();
        }
    }

    private void EmitCalibrationFocus()
    {
        // ターゲットデバイスが指定されていない場合は何もしない
        if (targetDevices == null || targetDevices.Count == 0) return;

        bool allTrue = true;
        foreach (var b in targetDevices) if (!b) allTrue = false;

        byte intensityVal = (byte)Mathf.Clamp(focusAmplitude * 255f, 0f, 255f);

        IDatagram targetDatagram;
        
        if (useMultiFocus && multiFocusPositions.Count > 0)
        {
            var activeFoci = new (AUTD3Sharp.Utils.Point3, AUTD3Sharp.Gain.Holo.Amplitude)[multiFocusPositions.Count];
            for (int i = 0; i < multiFocusPositions.Count; i++)
            {
                var p = multiFocusPositions[i];
                activeFoci[i] = (
                    new AUTD3Sharp.Utils.Point3(p.x + autdController.offset.x, p.y + autdController.offset.y, p.z + autdController.offset.z),
                    focusAmplitude * 10000f * Pa // キャリブレーション用は簡易的に最大10000Paにスケール
                );
            }
            targetDatagram = new AUTD3Sharp.Gain.Holo.GSPAT(activeFoci, new AUTD3Sharp.Gain.Holo.GSPATOption());
        }
        else
        {
            Vector3 pos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
            var p = new AUTD3Sharp.Utils.Point3(
                pos.x + autdController.offset.x, 
                pos.y + autdController.offset.y, 
                pos.z + autdController.offset.z
            );
            targetDatagram = new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) });
        }

        if (allTrue)
        {
            autdController.Send(targetDatagram);
        }
        else
        {
            var groupDict = new GroupDictionary();
            groupDict.Add("target", targetDatagram);
            groupDict.Add("null", new Null());

            // デバイスインデックスに応じて出力を切り替え
            autdController.SetGainGroup(dev => 
            {
                if (dev.Idx() < targetDevices.Count && targetDevices[dev.Idx()])
                    return "target";
                else
                    return "null";
            }, groupDict);
        }
    }

    /// <summary>
    /// 現在のこのオブジェクトのTransformをAUTDControllerのOffsetに適用し、位置をリセットします
    /// </summary>
    public void ApplyOffset()
    {
        if (autdController == null) return;
        
        autdController.offset += this.transform.localPosition;
        this.transform.localPosition = Vector3.zero;
        this.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 現在のFocusTargetと正解位置（truePositionTarget）の差分からオフセットを計算し適用します
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
    /// 現在のoffsetをTargetDevicesで選択されているAUTD3DeviceのTransformに永続的に反映（Bake）し、offsetをリセットします。
    /// （TargetPos_cmd = TargetPos + offset がデバイスからの相対距離となるため、デバイス自体を -offset 移動させることで同じ効果を得ます）
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
                // EditモードなどでUndoを登録する場合はEditorスクリプト側で行う
                devices[i].transform.position -= currentOffset;
                bakedCount++;
            }
        }

        autdController.offset = Vector3.zero;
        Debug.Log($"[Calibration] Baked offset {currentOffset} to {bakedCount} selected devices. (Device positions moved by {-currentOffset}). Offset reset to zero.");
    }
}
