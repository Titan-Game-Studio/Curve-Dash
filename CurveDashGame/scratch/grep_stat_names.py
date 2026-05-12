import os

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")
for i, line in enumerate(lines):
    if "m_StatName:" in line:
        print(f"Line {i+1}: {line.strip()}")
