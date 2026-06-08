import re

filepath_rnd = r'C:\Users\hongo\Documents\tsutsumi\RealTimeOcclusion\Assets\SRDisplayUnityPlugin\Runtime\SRDCoreRenderer.cs'

with open(filepath_rnd, 'r', encoding='utf-8', errors='surrogateescape') as f:
    content = f.read()

pattern = r'var cameraAdjuster = UnityEngine\.Object\.FindAnyObjectByType<CameraAdjuster>\(\);\s*bool isStereoMode = cameraAdjuster != null \? cameraAdjuster\.isStereoMode : false;'
repl = r'bool isStereoMode = _srdManager.IsStereoMode;'

new_content = re.sub(pattern, repl, content)

with open(filepath_rnd, 'w', encoding='utf-8', errors='surrogateescape') as f:
    f.write(new_content)

print('Updated SRDCoreRenderer.cs')
