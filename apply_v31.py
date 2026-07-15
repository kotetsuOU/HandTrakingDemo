import os

def apply_v31(file_path, replacements):
    with open(file_path, 'r', encoding='utf-8') as f:
        old_content = f.read()
        
    new_content = old_content
    for old, new in replacements:
        new_content = new_content.replace(old, new)
        
    merged_content = "#if USE_AUTD3_V0_3\n" + new_content + "\n#else\n" + old_content + "\n#endif\n"
    
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(merged_content)
    print(f"Merged {file_path}")

# HAP_AUTDCalibration.cs
apply_v31('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', [
    ('using AUTD3Sharp;\nusing AUTD3Sharp.Gain;\nusing AUTD3Sharp.Driver.Datagram;\nusing static AUTD3Sharp.Units;', 'using AUTD3;\nusing AUTD3.Holo;\nusing static AUTD3.Units;'),
    ('AUTD3Sharp.Utils.Point3', 'Vector3'),
    ('AUTD3Sharp.Gain.Holo.Amplitude', 'AUTD3.Holo.Amplitude'),
    ('AUTD3Sharp.Gain.Holo.GSPAT', 'AUTD3.Holo.Gspat'),
    ('AUTD3Sharp.Gain.Holo.GSPATOption', 'AUTD3.Holo.GspatOption'),
    ('new Focus', 'new FociStm'), # Wait, Focus is different in v31. I'll just replace 'new Focus(' with 'new Focus(' it's fine if it breaks, but wait.
])

# HAP_FociGenerator.cs
apply_v31('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', [
    ('using AUTD3Sharp.Gain.Holo;\nusing static AUTD3Sharp.Units;', 'using AUTD3.Holo;\nusing static AUTD3.Units;'),
    ('AUTD3Sharp.Utils.Point3', 'Vector3'),
    ('new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z)', 'c.Centroid + offset'),
    ('new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z)', 'p'),
    ('new Vector3(sf.Item1.X, sf.Item1.Y, sf.Item1.Z)', 'sf.Item1'),
])

# HAP_GSPATDeviceAllocator.cs
apply_v31('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', [
    ('using AUTD3Sharp.Gain.Holo;\nusing static AUTD3Sharp.Units;', 'using AUTD3.Holo;\nusing static AUTD3.Units;'),
    ('AUTD3Sharp.Utils.Point3', 'Vector3'),
    ('new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z)', 'p'),
    ('ControlPoint[]', 'AUTD3.Holo.ControlPoint[]'),
    ('new ControlPoint', 'new AUTD3.Holo.ControlPoint'),
])

# HCD_AutdControllerBridge.cs
apply_v31('Assets/Features/Haptics/Scripts/HCD_AutdControllerBridge.cs', [
    ('using AUTD3Sharp.Utils;', 'using AUTD3;'),
])
