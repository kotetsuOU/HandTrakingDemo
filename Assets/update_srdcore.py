import re
import os

filepath = r'C:\Users\hongo\Documents\tsutsumi\RealTimeOcclusion\Assets\SRDisplayUnityPlugin\Runtime\SRDCoreRenderer.cs'

with open(filepath, 'r', encoding='utf-8', errors='surrogateescape') as f:
    content = f.read()

pattern = r'(if \(_srdManager\.UseDirectGpuImageBuffer\)\s*\{\s*//.*?if \(_srdManager\.DirectGpuImageMap != null\)\s*\{\s*_stereoCompositer\.RegisterSourceStereoTextures\(_srdManager\.DirectGpuImageMap, _srdManager\.DirectGpuImageMap\);\s*_stereoCompositer\.RenderStereoComposition\(_outputTexture\);\s*_isStereoTextureRegistered = false;.*?\}\s*\})'

repl = r'''if (_srdManager.UseDirectGpuImageBuffer)
            {
                var cameraAdjuster = UnityEngine.Object.FindAnyObjectByType<CameraAdjuster>();
                bool isStereoMode = cameraAdjuster != null ? cameraAdjuster.isStereoMode : false;

                if (isStereoMode)
                {
                    if (_srdManager.DirectGpuImageMapLeft != null && _srdManager.DirectGpuImageMapRight != null)
                    {
                        _stereoCompositer.RegisterSourceStereoTextures(_srdManager.DirectGpuImageMapLeft, _srdManager.DirectGpuImageMapRight);
                        _stereoCompositer.RenderStereoComposition(_outputTexture);
                        _isStereoTextureRegistered = false;
                    }
                }
                else
                {
                    if (_srdManager.DirectGpuImageMap != null)
                    {
                        _stereoCompositer.RegisterSourceStereoTextures(_srdManager.DirectGpuImageMap, _srdManager.DirectGpuImageMap);
                        _stereoCompositer.RenderStereoComposition(_outputTexture);
                        _isStereoTextureRegistered = false;
                    }
                }
            }'''

new_content = re.sub(pattern, repl, content, flags=re.DOTALL)

with open(filepath, 'w', encoding='utf-8', errors='surrogateescape') as f:
    f.write(new_content)

print('Updated SRDCoreRenderer.cs')
