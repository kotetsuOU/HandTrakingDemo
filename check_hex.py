with open('Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs', 'rb') as f:
    data = f.read()
lines = data.split(b'\n')
for i, line in enumerate(lines[63:68]):
    print(f"{i+64}: {line}")
