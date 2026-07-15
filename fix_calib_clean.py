# -*- coding: utf-8 -*-
import os

with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')
old_code_lines = []
for line in lines:
    if line.startswith('#if') or line.startswith('#else') or line.startswith('#endif'):
        continue
    old_code_lines.append(line)

old_code = '\n'.join(old_code_lines)
# Wait, this is dangerous if the file already has #else.
# Let's extract the Old code from the #else block!
parts = content.split('#else')
if len(parts) == 2:
    old_code = parts[1].replace('#endif', '')
else:
    old_code = parts[0].replace('#if !USE_AUTD3_LEGACY', '').replace('#endif', '')

new_code = """
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#nullable enable

public class HAP_AUTDCalibration : MonoBehaviour
{
    public HAP_AUTDController autdController = null!;

    [Header("Calibration Mode")]
    [Tooltip("有効化すると通常のHaptics出力をバイパスし、この設定に基づぁEチEトE力を行います、E")]
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
        if (autdController == null || !autdController.isInitialized) return;
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
}
"""

merged = "#if USE_AUTD3_LEGACY\n" + old_code + "\n#else\n" + new_code + "\n#endif\n"

with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'w', encoding='utf-8') as f:
    f.write(merged)
print("Rewrote HAP_AUTDCalibration.cs cleanly")
