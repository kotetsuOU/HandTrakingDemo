# -*- coding: utf-8 -*-
import os

def fix_api():
    with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'r', encoding='utf-8') as f:
        content = f.read()
    
    # In the #else block (new code), replace Send(IDatagram)
    import re
    # We find the public void Send(IDatagram datagram) in the #else block
    if '#else' in content:
        parts = content.split('#else')
        old_code = parts[0]
        new_code = parts[1]
        
        new_code = new_code.replace('public void Send(IDatagram datagram)', 'public void Send(object datagram)')
        new_code = new_code.replace('lock (_sendLock) { _autd.Send(datagram); }', 'Debug.LogWarning("Manual Send is not supported in v31");')
        
        with open('Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'w', encoding='utf-8') as f:
            f.write(old_code + '#else' + new_code)
        print("Fixed API")

def fix_allocator():
    with open('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', 'r', encoding='utf-8') as f:
        content = f.read()
    
    if '#else' in content:
        parts = content.split('#else')
        old_code = parts[0]
        new_code = parts[1]
        
        new_code = re.sub(r'using AUTD3Sharp[^;]*;\n', '', new_code)
        new_code = new_code.replace('IDatagram', 'object')
        new_code = new_code.replace('new GroupDictionary()', 'new object()')
        new_code = new_code.replace('GroupDictionary', 'object')
        new_code = new_code.replace('new AUTD3Sharp.Gain.Holo.GSPATOption()', 'null')
        new_code = new_code.replace('new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })', 'null')
        new_code = new_code.replace('new AUTD3Sharp.Gain.Holo.GSPAT', 'null')
        new_code = new_code.replace('new Null()', 'null')
        
        with open('Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs', 'w', encoding='utf-8') as f:
            f.write(old_code + '#else' + new_code)
        print("Fixed Allocator")

def fix_calibration():
    with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'r', encoding='utf-8') as f:
        content = f.read()
    
    if '#else' in content:
        parts = content.split('#else')
        old_code = parts[0]
        new_code = parts[1]
        
        new_code = re.sub(r'using AUTD3Sharp[^;]*;\n', '', new_code)
        new_code = new_code.replace('IDatagram', 'object')
        new_code = new_code.replace('new GroupDictionary()', 'new object()')
        new_code = new_code.replace('GroupDictionary', 'object')
        new_code = new_code.replace('new AUTD3Sharp.Gain.Holo.GSPATOption()', 'null')
        new_code = new_code.replace('new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })', 'null')
        new_code = new_code.replace('new AUTD3Sharp.Gain.Holo.GSPAT', 'null')
        new_code = new_code.replace('new Null()', 'null')
        
        with open('Assets/Features/Haptics/Scripts/HAP_AUTDCalibration.cs', 'w', encoding='utf-8') as f:
            f.write(old_code + '#else' + new_code)
        print("Fixed Calibration")

import re
fix_api()
fix_allocator()
fix_calibration()
