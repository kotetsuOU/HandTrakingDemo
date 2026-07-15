# -*- coding: utf-8 -*-
import os

with open('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', 'r', encoding='utf-8') as f:
    old_code = f.read()

new_code = """
using System.Collections.Generic;
using UnityEngine;

#nullable enable

public static class HAP_GSPATDeviceAllocator
{
    public static void Allocate(
        object builder,
        object geometry,
        List<HAP_FociGenerator.ClusterFociData> clusterData,
        List<AUTD3Device> connectedDevices,
        HoloAlgorithm holoAlgorithm,
        bool enableDirectionalGrouping,
        float directionalAngleThreshold,
        float focusIntensityPascal,
        HAP_AUTDDebugDisabler? debugDisabler = null)
    {
        Debug.LogWarning("HAP_GSPATDeviceAllocator is not fully implemented for v31 yet.");
    }
}
"""

merged_content = "#if USE_AUTD3_LEGACY\n" + old_code + "\n#else\n" + new_code + "\n#endif\n"

with open('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', 'w', encoding='utf-8') as f:
    f.write(merged_content)

print("Rewrote HAP_GSPATDeviceAllocator.cs cleanly")
