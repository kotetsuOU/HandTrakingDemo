import os
import glob

def replace_in_files():
    files = glob.glob('Assets/Features/Haptics/Scripts/*.cs')
    for f in files:
        with open(f, 'r', encoding='utf-8') as file:
            content = file.read()
            
        new_content = content.replace('#if !USE_AUTD3_V0_3', '#if USE_AUTD3_LEGACY')
        new_content = new_content.replace('#if USE_AUTD3_V0_3', '#if !USE_AUTD3_LEGACY')
        
        if content != new_content:
            with open(f, 'w', encoding='utf-8') as file:
                file.write(new_content)
            print(f"Updated {f}")

replace_in_files()
