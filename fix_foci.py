# -*- coding: utf-8 -*-
import os
import re

with open('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', 'r', encoding='utf-8') as f:
    content = f.read()

parts = content.split('#else')
if len(parts) == 2:
    new_code = parts[0]
    old_code = parts[1]

    # Replace * Pa with Amplitude.FromPascal(...)
    new_code = re.sub(r'\(([^)]+)\)\s*\*\s*Pa', r'Amplitude.FromPascal(\1)', new_code)
    
    merged = new_code + '#else' + old_code
    with open('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', 'w', encoding='utf-8') as f:
        f.write(merged)
    print("Fixed HAP_FociGenerator.cs")
else:
    print("Could not find #else in HAP_FociGenerator.cs")
