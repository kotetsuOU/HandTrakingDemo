import re

filepath_rg = r'C:\Users\hongo\Documents\tsutsumi\RealTimeOcclusion\Assets\Scripts\ParallaxBarrier\Rendering\Occlusion\PCD_RenderPass_RenderGraph.cs'

with open(filepath_rg, 'r', encoding='utf-8', errors='surrogateescape') as f:
    content = f.read()

pattern = r'(isLeftEye = \(cameraData\.camera == cameraAdjuster\.leftEyeCamera\);\s*\}\s*\})'
repl = r'\1\n            if (srdManager != null) srdManager.IsStereoMode = isStereoMode;'

new_content = re.sub(pattern, repl, content)

with open(filepath_rg, 'w', encoding='utf-8', errors='surrogateescape') as f:
    f.write(new_content)

print('Updated PCD_RenderPass_RenderGraph.cs')
