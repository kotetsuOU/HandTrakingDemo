# -*- coding: utf-8 -*-
import os

with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'r', encoding='utf-8') as f:
    content = f.read()

parts = content.split('#else')
if len(parts) == 2:
    new_code = parts[0]
    old_code = parts[1]

    # Fix SetNull
    new_code = new_code.replace('lock (_sendLock) { _autd.Send(new Null()); }', 'Debug.LogWarning("SetNull is not supported in v31 yet");')

    # Fix Focus
    new_code = new_code.replace('new Focus(new Vector3(p.x, p.y, p.z), new FocusOption { Intensity = new Intensity(intensityVal) })', 'null /* Focus not implemented in v31 */')

    # Fix Vector3 Amplitude conversion
    new_code = new_code.replace('var f = new AUTD3.Holo.ControlPoint[foci.Length];', 'var f = new (Vector3, AUTD3.Holo.Amplitude)[foci.Length];')
    new_code = new_code.replace('f[i] = new AUTD3.Holo.ControlPoint(new Vector3(p.x, p.y, p.z), Amplitude.FromPascal(focusAmplitude * 10000f));', 'f[i] = (new Vector3(p.x, p.y, p.z), Amplitude.FromPascal(focusAmplitude * 10000f));')

    merged = new_code + '#else' + old_code
    with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'w', encoding='utf-8') as f:
        f.write(merged)
    print("Fixed HAP_AUTDController_API.cs")
else:
    print("Could not find #else in HAP_AUTDController_API.cs")
