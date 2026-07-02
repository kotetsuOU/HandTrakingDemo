import re  
import sys  
files = ['Assets/Features/Haptics/Scripts/HAP_AUTDController_API.cs', 'Assets/Features/Haptics/Scripts/HAP_AUTDController_Config.cs']  
for f in files:  
    with open(f, 'r', encoding='utf-8') as file:  
        content = file.read()  
    content = re.sub(r'(_autd\.Send\((.*?)\);)', r'lock (_sendLock) { \1 }', content)  
    with open(f, 'w', encoding='utf-8') as file:  
        file.write(content)  
