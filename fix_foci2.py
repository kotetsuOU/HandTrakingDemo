# -*- coding: utf-8 -*-
import re

with open('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('CalculateAmplitudeAmplitude', 'CalculateAmplitude')
content = content.replace('.FromPascal(c)', '(c))')
# Wait, replacing CalculateAmplitudeAmplitude.FromPascal(c) -> Amplitude.FromPascal(centroidSource.CalculateAmplitude(c))
content = re.sub(r'centroidSource\.CalculateAmplitudeAmplitude\.FromPascal\((c|sf\.Item2)\)', r'Amplitude.FromPascal(centroidSource.CalculateAmplitude(\1))', content)

# And fix any leftover * Pa that I might have missed
# content = re.sub(r'\(([^)]+)\)\s*\*\s*Pa', r'Amplitude.FromPascal(\1)', content)

with open('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', 'w', encoding='utf-8') as f:
    f.write(content)

with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'r', encoding='utf-8') as f:
    calib = f.read()
calib = calib.replace('!autdController.isInitialized', 'autdController == null')
with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'w', encoding='utf-8') as f:
    f.write(calib)

print("Fixed Foci and Calib")
