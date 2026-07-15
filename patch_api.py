# -*- coding: utf-8 -*-
import os

with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'r', encoding='utf-8') as f:
    old_content = f.read()

new_content = old_content
new_content = new_content.replace(
    'using AUTD3Sharp;\nusing AUTD3Sharp.Gain;\nusing AUTD3Sharp.Modulation;\nusing AUTD3Sharp.Gain.Holo;\nusing AUTD3Sharp.Driver.Datagram;\nusing static AUTD3Sharp.Units;',
    'using AUTD3;\nusing AUTD3.Holo;\nusing static AUTD3.Units;'
)
new_content = new_content.replace('AUTD3Sharp.Utils.Point3', 'Vector3')
new_content = new_content.replace('if (_autd == null) return;', 'if (_client == null || geometry == null) return;')
new_content = new_content.replace(
    'public void SetFocus(Vector3 position, float amplitude = 1f)\n    {\n        if (_client == null || geometry == null) return;\n        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);\n        var p = new Vector3(position.x + offset.x, position.y + offset.y, position.z + offset.z);\n        lock (_sendLock) { _autd.Send(new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })); }\n        _isCurrentlyOff = false;\n    }',
    'public async void SetFocus(Vector3 position, float amplitude = 1f)\n    {\n        if (_client == null || geometry == null) return;\n        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);\n        var p = new Vector3(position.x + offset.x, position.y + offset.y, position.z + offset.z);\n        \n        using var builder = _client.DatagramBuilder();\n        builder.Push(new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) }));\n        using var frames = builder.Build();\n        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }\n        _isCurrentlyOff = false;\n    }'
)

new_content = new_content.replace(
    'public void SetHolo(IEnumerable<Vector3> positions, IEnumerable<float> amplitudesPa, HoloAlgorithm algorithm = HoloAlgorithm.GSPAT)',
    'public async void SetHolo(IEnumerable<Vector3> positions, IEnumerable<float> amplitudesPa, HoloAlgorithm algorithm = HoloAlgorithm.GSPAT)'
)
new_content = new_content.replace(
    'var activeFoci = new (Vector3, AUTD3Sharp.Gain.Holo.Amplitude)[posArray.Length];',
    'var activeFoci = new AUTD3.Holo.ControlPoint[posArray.Length];'
)
new_content = new_content.replace(
    'activeFoci[i] = (\n                new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z),\n                ampArray[i] * Pa\n            );',
    'activeFoci[i] = new AUTD3.Holo.ControlPoint(\n                new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z),\n                Amplitude.FromPascal(ampArray[i])\n            );'
)
new_content = new_content.replace(
    'if (algorithm == HoloAlgorithm.GSPAT)\n            lock (_sendLock) { _autd.Send(new GSPAT(activeFoci, new GSPATOption())); }\n        else\n            lock (_sendLock) { _autd.Send(new Naive(activeFoci, new NaiveOption())); }',
    'using var builder = _client.DatagramBuilder();\n        var buffer = geometry.PatternBuffer();\n        var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));\n        \n        if (algorithm == HoloAlgorithm.GSPAT)\n            AUTD3.Holo.Holo.Gspat(geometry, activeFoci, wavelength, new GspatOption(), buffer);\n        else\n            AUTD3.Holo.Holo.Naive(geometry, activeFoci, wavelength, new NaiveOption(), buffer);\n            \n        builder.Push(new Pattern(PatternBank.B0, buffer));\n        using var frames = builder.Build();\n        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }'
)

new_content = new_content.replace(
    'public void SetFocusStm(IEnumerable<Vector3> positions, float frequency, float amplitude = 1f)',
    'public async void SetFocusStm(IEnumerable<Vector3> positions, float frequency, float amplitude = 1f)'
)
new_content = new_content.replace(
    'var foci = positions.Select(p => \n            new ControlPoints(new[] { new ControlPoint(new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z)) }, intensity)\n        ).ToArray();\n\n        lock (_sendLock) { _autd.Send(new FociSTM(foci, frequency * Hz)); }',
    'var points = positions.Select(p => \n            new AUTD3.ControlPoints(new[] { new AUTD3.ControlPoint(new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z)) })\n        ).ToArray();\n\n        var stm = new FociStm(frequency * Hz, points);\n\n        using var builder = _client.DatagramBuilder();\n        builder.Push(stm);\n        using var frames = builder.Build();\n        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }'
)

new_content = new_content.replace(
    'var fociSTM = new List<ControlPoints>();\n        foreach(var frame in frames)\n        {\n            var points = frame.Select(p => new ControlPoint(new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z))).ToArray();\n            fociSTM.Add(new ControlPoints(points, intensity));\n        }\n        \n        lock (_sendLock) { _autd.Send(new FociSTM(fociSTM, frequency * Hz)); }\n        _isCurrentlyOff = false;',
    'Debug.LogWarning("SetMultiFocusStm requires GainSTM in v31, not yet implemented here.");'
)

new_content = new_content.replace(
    'public void SetGainStm(IEnumerable<IGain> frames, float frequency, GainSTMMode? modeOverride = null)\n    {\n        if (_client == null || geometry == null) return;\n        var mode = modeOverride ?? gainStmMode;\n        lock (_sendLock) { _autd.Send(new GainSTM(frames, frequency * Hz, new GainSTMOption { Mode = mode })); }\n        _isCurrentlyOff = false;\n    }',
    'public void SetGainStm(IEnumerable<ICommand> frames, float frequency)\n    {\n        Debug.LogWarning("SetGainStm requires new v31 API, not yet implemented here.");\n    }'
)

new_content = new_content.replace(
    'public void SetCustomGain(Func<Device, Func<Transducer, Drive>> f)\n    {\n        if (_client == null || geometry == null) return;\n        lock (_sendLock) { _autd.Send(new AUTD3Sharp.Gain.Custom(f)); }\n        _isCurrentlyOff = false;\n    }',
    'public void SetCustomGain(Func<Device, Func<object, Emission>> f)\n    {\n        Debug.LogWarning("SetCustomGain requires new v31 API, not yet implemented here.");\n    }'
)

new_content = new_content.replace(
    'public void SetGainGroup(Func<Device, object?> keyMap, GroupDictionary datagramMap)\n    {\n        if (_client == null || geometry == null) return;\n        lock (_sendLock) { _autd.Send(new Group(keyMap, datagramMap)); }\n        _isCurrentlyOff = false;\n    }',
    'public void SetGainGroup()\n    {\n        Debug.LogWarning("SetGainGroup requires new v31 API, not yet implemented here.");\n    }'
)

new_content = new_content.replace(
    'public void SetCustomModulation(byte[] buffer, uint frequency)\n    {\n        if (_client == null || geometry == null) return;\n        // \u57fa\u672c\u5468\u6ce2\u6570 = \u30b5\u30f3\u30d7\u30ea\u30f3\u30b0\u5468\u6ce2\u6570 / \u30d0\u30c3\u30d5\u30a1\u9577\n        lock (_sendLock) { _autd.Send(new AUTD3Sharp.Modulation.Custom(buffer, (frequency * buffer.Length) * Hz)); }\n    }',
    'public void SetCustomModulation(byte[] buffer, uint frequency)\n    {\n        Debug.LogWarning("SetCustomModulation requires new v31 API, not yet implemented here.");\n    }'
)

merged_content = "#if USE_AUTD3_LEGACY\n" + old_content + "\n#else\n" + new_content + "\n#endif\n"

with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'w', encoding='utf-8') as f:
    f.write(merged_content)

print("Patched HAP_AUTDController_API.cs")

