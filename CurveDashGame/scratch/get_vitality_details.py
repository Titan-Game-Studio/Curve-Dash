import os

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

blocks = content.split("--- !u!")
for block in blocks:
    if "&-4240061784426487929" in block:
        print(block)
        break
