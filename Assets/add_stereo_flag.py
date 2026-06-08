import re

filepath_mgr = r'C:\Users\hongo\Documents\tsutsumi\RealTimeOcclusion\Assets\SRDisplayUnityPlugin\Runtime\SRDManager.cs'

with open(filepath_mgr, 'r', encoding='utf-8', errors='surrogateescape') as f:
    content = f.read()

pattern = r'(\[HideInInspector\] public RenderTexture DirectGpuImageMapRight;)'
repl = r'\1\n        [HideInInspector] public bool IsStereoMode = false;'

new_content = re.sub(pattern, repl, content)

with open(filepath_mgr, 'w', encoding='utf-8', errors='surrogateescape') as f:
    f.write(new_content)

print('Updated SRDManager.cs')
