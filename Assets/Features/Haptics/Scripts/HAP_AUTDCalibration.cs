#if USE_AUTD3_LEGACY

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Logging;
using Features.Haptics.Debug;
using static AUTD3Sharp.Units;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDHapticsController hapticsController = null!;
    public HAP_AUTDHardwareController hardwareController = null!;
    public HAP_AUTDTransformLoader transformLoader = null!;

    [Header("Calibration Mode")]
    [Tooltip("When enabled, bypasses normal Haptics output and emits calibration focus based on the settings below.")]
    public bool enableCalibration = false;

    [Header("Target Devices")]
    [Tooltip("Toggle each AUTD device to include or exclude it from calibration output.")]
    public List<bool> targetDevices = new List<bool>();

    [Header("Focus Settings")]
    public bool useMultiFocus = false;
    [Tooltip("When a Transform is assigned, its world position is used as the focus point instead of singleFocusPosition.")]
    public Transform? singleFocusTarget;
    public Vector3 singleFocusPosition = Vector3.zero;
    public List<Vector3> multiFocusPositions = new List<Vector3> { Vector3.zero };
    
    [Tooltip("When calibrating, the Transform world position here is used as the 'true' reference position for ApplyOffsetByDifference.")]
    public Transform? truePositionTarget;
    
    [Range(0f, 1f)]
    public float focusAmplitude = 1f;

    void Awake()
    {
        if (hapticsController == null) hapticsController = FindAnyObjectByType<HAP_AUTDHapticsController>();
        if (hardwareController == null) hardwareController = FindAnyObjectByType<HAP_AUTDHardwareController>();
        if (transformLoader == null)
        {
            if (hapticsController != null && hapticsController.transformLoader != null) transformLoader = hapticsController.transformLoader;
            else transformLoader = FindAnyObjectByType<HAP_AUTDTransformLoader>();
        }
    }

    void Update()
    {
        if (hapticsController == null || hardwareController == null) return;

        hapticsController.bypassHaptics = enableCalibration;

        if (enableCalibration)
        {
            EmitCalibrationFocus();
        }
    }

    private void EmitCalibrationFocus()
    {
        if (targetDevices == null || targetDevices.Count == 0 || hardwareController == null) return;

        var connectedDevices = hardwareController.ConnectedDevices;
        bool allTrue = targetDevices.Count > 0 && targetDevices.Count == connectedDevices.Count;
        foreach (var b in targetDevices) if (!b) allTrue = false;

        byte intensityVal = (byte)Mathf.Clamp(focusAmplitude * 255f, 0f, 255f);
        Vector3 offset = transformLoader != null ? transformLoader.offset : Vector3.zero;

        AUTD3Sharp.Driver.Datagram.IDatagram targetDatagram;
        
        if (useMultiFocus && multiFocusPositions.Count > 0)
        {
            var activeFoci = new (AUTD3Sharp.Utils.Point3, AUTD3Sharp.Gain.Holo.Amplitude)[multiFocusPositions.Count];
            for (int i = 0; i < multiFocusPositions.Count; i++)
            {
                var p = multiFocusPositions[i];
                activeFoci[i] = (
                    new AUTD3Sharp.Utils.Point3(p.x + offset.x, p.y + offset.y, p.z + offset.z),
                    focusAmplitude * 10000f * Pa
                );
            }
            targetDatagram = new AUTD3Sharp.Gain.Holo.GSPAT(activeFoci, new AUTD3Sharp.Gain.Holo.GSPATOption());
        }
        else
        {
            Vector3 pos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
            var p = new AUTD3Sharp.Utils.Point3(
                pos.x + offset.x, 
                pos.y + offset.y, 
                pos.z + offset.z
            );
            targetDatagram = new AUTD3Sharp.Gain.Focus(p, new AUTD3Sharp.Gain.FocusOption { Intensity = new AUTD3Sharp.Intensity(intensityVal) });
        }

        var debugDisabler = hapticsController != null ? hapticsController.debugDisabler : null;
        bool hasDisabledDevice = debugDisabler != null && connectedDevices.Any(d => debugDisabler.IsDisabled(d.ID));
        
        if (allTrue && !hasDisabledDevice)
        {
            hardwareController.Send(targetDatagram);
        }
        else
        {
            var groupDict = new AUTD3Sharp.GroupDictionary();
            groupDict.Add("target", targetDatagram);
            groupDict.Add("null", new AUTD3Sharp.Gain.Null());

            string[] mapping = new string[connectedDevices.Count];
            for (int i = 0; i < connectedDevices.Count; i++) {
                if (connectedDevices[i] == null) {
                    mapping[i] = "null";
                    continue;
                }
                var deviceId = connectedDevices[i].ID;
                if (debugDisabler != null && debugDisabler.IsDisabled(deviceId)) {
                    mapping[i] = "null";
                } else if (i < targetDevices.Count && targetDevices[i]) {
                    mapping[i] = "target";
                } else {
                    mapping[i] = "null";
                }
            }

            var groupDatagram = new AUTD3Sharp.Group(dev => 
            {
                int deviceIndex = dev.Idx();
                if (deviceIndex < 0 || deviceIndex >= mapping.Length) return "null";
                return mapping[deviceIndex];
            }, groupDict);

            hardwareController.Send(groupDatagram);
        }
    }

    /// <summary>
    /// 現在のこのオブジェクトのTransformをTransformLoaderのOffsetに適用し、位置をリセットします。
    /// </summary>
    public void ApplyOffset()
    {
        if (transformLoader == null) return;
        
        transformLoader.offset += this.transform.localPosition;
        this.transform.localPosition = Vector3.zero;
        this.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 現在のFocusTargetと正解位置（truePositionTarget）の差分からオフセットを計算し適用します。
    /// </summary>
    public void ApplyOffsetByDifference()
    {
        if (transformLoader == null) return;
        
        Vector3 focusPos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
        
        if (truePositionTarget == null)
        {
            AppLogger.LogWarning(this, HAP_LogTriggers.TagCalibration, "truePositionTarget is not set. Cannot apply difference.");
            return;
        }

        Vector3 diff = focusPos - truePositionTarget.position;
        transformLoader.offset += diff;
        
        AppLogger.Log(this, HAP_LogTriggers.TagCalibration, $"Applied offset by difference: {diff}. New Offset: {transformLoader.offset}");
    }

    /// <summary>
    /// 現在のoffsetをTargetDevicesで選択されているAUTD3DeviceのTransformに永続的に反映（Bake）し、offsetをリセットします。
    /// </summary>
    public void BakeOffsetToDevices()
    {
        if (transformLoader == null) return;

        Vector3 currentOffset = transformLoader.offset;
        if (currentOffset == Vector3.zero)
        {
            AppLogger.Log(this, HAP_LogTriggers.TagCalibration, "Offset is already zero. Nothing to bake.");
            return;
        }

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None).OrderBy(d => d.ID).ToArray();
        if (devices.Length == 0)
        {
            AppLogger.LogWarning(this, HAP_LogTriggers.TagCalibration, "No AUTD3Device found in the scene to bake to.");
            return;
        }

        int bakedCount = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            if (i < targetDevices.Count && targetDevices[i])
            {
                devices[i].transform.position -= currentOffset;
                bakedCount++;
            }
        }

        transformLoader.offset = Vector3.zero;
        AppLogger.Log(this, HAP_LogTriggers.TagCalibration, $"Baked offset {currentOffset} to {bakedCount} selected devices. (Device positions moved by {-currentOffset}). Offset reset to zero.");
    }
}

#else

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Logging;
using Features.Haptics.Debug;
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;
using HoloCP = AUTD3.Holo.ControlPoint;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDHapticsController autdController = null!;
    public HAP_AUTDTransformLoader transformLoader = null!;

    [Header("Calibration Mode")]
    [Tooltip("有効化すると通常のHaptics出力をバイパスし、この設定に基づき出力を行います")]
    public bool enableCalibration = false;

    [Header("Target Devices")]
    [Tooltip("出力対象とするAUTDデバイスのインデックス")]
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

    [Tooltip("キャリブレーション時の正解位置（実際に焦点が合っているべき物理的な位置）")]
    public Transform? truePositionTarget;

    private System.Threading.Tasks.Task? _calibrationTask = null;

    void Awake()
    {
        if (autdController == null) autdController = FindAnyObjectByType<HAP_AUTDHapticsController>();
        if (transformLoader == null)
        {
            if (autdController != null && autdController.transformLoader != null) transformLoader = autdController.transformLoader;
            else transformLoader = FindAnyObjectByType<HAP_AUTDTransformLoader>();
        }
    }

    void Update()
    {
        if (autdController == null) return;
        autdController.bypassHaptics = enableCalibration;
        if (enableCalibration)
        {
            if (_calibrationTask == null || _calibrationTask.IsCompleted)
            {
                _calibrationTask = EmitCalibrationFocusAsync();
            }
        }
    }

    private async System.Threading.Tasks.Task EmitCalibrationFocusAsync()
    {
        if (autdController == null || autdController.client == null || autdController.geometry == null) return;
        if (targetDevices == null || targetDevices.Count == 0) return;

        var client = autdController.client;
        var geometry = autdController.geometry;
        Vector3 offset = transformLoader != null ? transformLoader.offset : Vector3.zero;

        var activeFoci = new List<HoloCP>();
        if (useMultiFocus && multiFocusPositions.Count > 0)
        {
            foreach (var p in multiFocusPositions)
            {
                activeFoci.Add(new HoloCP(
                    new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z),
                    Amplitude.FromPascal(focusAmplitude * 10000f)
                ));
            }
        }
        else
        {
            Vector3 pos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
            activeFoci.Add(new HoloCP(
                new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
                Amplitude.FromPascal(focusAmplitude * 10000f)
            ));
        }

        var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));
        using var builder = client.DatagramBuilder();

        builder.PushEach(deviceIndex =>
        {
            if (deviceIndex < 0 || deviceIndex >= targetDevices.Count) return null;
            if (!targetDevices[deviceIndex]) return null;

            bool[][] maskArray = new bool[geometry.NumDevices][];
            for (int d = 0; d < geometry.NumDevices; d++)
            {
                maskArray[d] = new bool[geometry[d].NumTransducers];
                if (d == deviceIndex)
                {
                    for (int t = 0; t < maskArray[d].Length; t++) maskArray[d][t] = true;
                }
            }
            var mask = TransducerMask.Masked(maskArray);

            var buffer = geometry.PatternBuffer();
            var option = new GspatOption(repeat: 100, constraint: null, directivity: Directivity.Sphere, backend: default, mask: mask);
            AUTD3.Holo.Holo.Gspat(geometry, activeFoci.ToArray(), wavelength, option, buffer);

            return new Pattern(PatternBank.B0, buffer);
        });

        using var frames = builder.Build();
        foreach (var frame in frames)
        {
            await client.SendCheckedAsync(frame);
        }
    }

    /// <summary>
    /// 現在のこのオブジェクトのTransformをTransformLoaderのOffsetに適用し、位置をリセットします。
    /// </summary>
    public void ApplyOffset()
    {
        if (transformLoader == null) return;
        
        transformLoader.offset += this.transform.localPosition;
        this.transform.localPosition = Vector3.zero;
        this.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 現在のFocusTargetと正解位置（truePositionTarget）の差分からオフセットを計算し適用します。
    /// </summary>
    public void ApplyOffsetByDifference()
    {
        if (transformLoader == null) return;
        
        Vector3 focusPos = singleFocusTarget != null ? singleFocusTarget.position : singleFocusPosition;
        
        if (truePositionTarget == null)
        {
            AppLogger.LogWarning(this, HAP_LogTriggers.TagCalibration, "truePositionTarget is not set. Cannot apply difference.");
            return;
        }

        Vector3 diff = focusPos - truePositionTarget.position;
        transformLoader.offset += diff;
        
        AppLogger.Log(this, HAP_LogTriggers.TagCalibration, $"Applied offset by difference: {diff}. New Offset: {transformLoader.offset}");
    }

    /// <summary>
    /// 現在のoffsetをTargetDevicesで選択されているAUTD3DeviceのTransformに永続的に反映（Bake）し、offsetをリセットします。
    /// </summary>
    public void BakeOffsetToDevices()
    {
        if (transformLoader == null) return;

        Vector3 currentOffset = transformLoader.offset;
        if (currentOffset == Vector3.zero)
        {
            AppLogger.Log(this, HAP_LogTriggers.TagCalibration, "Offset is already zero. Nothing to bake.");
            return;
        }

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None).OrderBy(d => d.ID).ToArray();
        if (devices.Length == 0)
        {
            AppLogger.LogWarning(this, HAP_LogTriggers.TagCalibration, "No AUTD3Device found in the scene to bake to.");
            return;
        }

        int bakedCount = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            if (i < targetDevices.Count && targetDevices[i])
            {
                devices[i].transform.position -= currentOffset;
                bakedCount++;
            }
        }

        transformLoader.offset = Vector3.zero;
        AppLogger.Log(this, HAP_LogTriggers.TagCalibration, $"Baked offset {currentOffset} to {bakedCount} selected devices. (Device positions moved by {-currentOffset}). Offset reset to zero.");
    }
}

#endif
