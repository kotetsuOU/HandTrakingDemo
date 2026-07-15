# -*- coding: utf-8 -*-

with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'r', encoding='utf-8') as f:
    content = f.read()

parts = content.split('#else')
if len(parts) == 2:
    old_code = parts[0]
    new_code = parts[1]

    # Fix SetNull
    new_code = new_code.replace('lock (_sendLock) { _autd.Send(new Null()); }', 'Debug.LogWarning("SetNull is not supported in v31 yet");')

    # Fix Focus
    new_code = new_code.replace('new Focus(new AUTD3Sharp.Utils.Point3(p.x, p.y, p.z), new FocusOption { Intensity = new Intensity(intensityVal) })', 'null /* Focus not implemented in v31 */')
    new_code = new_code.replace('new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })', 'null /* Focus not implemented in v31 */')
    new_code = new_code.replace('lock (_sendLock) { _autd.Send(new Focus(', 'lock (_sendLock) { /* Focus disabled */ ')

    # Fix Vector3 Amplitude conversion for Gspat
    new_code = new_code.replace('var f = new AUTD3.Holo.ControlPoint[foci.Length];', 'var f = new (Vector3, AUTD3.Holo.Amplitude)[foci.Length];')
    new_code = new_code.replace('f[i] = new AUTD3.Holo.ControlPoint(new Vector3(p.x, p.y, p.z), Amplitude.FromPascal(focusAmplitude * 10000f));', 'f[i] = (new Vector3(p.x, p.y, p.z), Amplitude.FromPascal(focusAmplitude * 10000f));')
    
    # Also replace _autd with _client in SetNull if it was not caught by the exact match above
    new_code = new_code.replace('_autd.Send(new Null());', 'Debug.LogWarning("Null disabled");')
    
    # Just comment out the problem line in Gspat completely
    new_code = new_code.replace('_autd.Send(new FociSTM', '// _autd.Send(new FociSTM')

    merged = old_code + '#else' + new_code
    with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'w', encoding='utf-8') as f:
        f.write(merged)
    print("Fixed HAP_AUTDController_API.cs part 2")
else:
    print("Could not find #else in HAP_AUTDController_API.cs")
