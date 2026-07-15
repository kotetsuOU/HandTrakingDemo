# -*- coding: utf-8 -*-
import os

with open('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# content currently has #if !USE_AUTD3_LEGACY at the top and #endif at the bottom.
# We will extract everything between them as the "old_code"
lines = content.split('\n')
old_code_lines = []
for line in lines:
    if line.startswith('#if') or line.startswith('#endif'):
        continue
    old_code_lines.append(line)

old_code = '\n'.join(old_code_lines)

# Now we construct the file properly!
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

print("Rewrote HAP_GSPATDeviceAllocator.cs")
