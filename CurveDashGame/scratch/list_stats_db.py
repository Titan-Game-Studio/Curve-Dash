import os

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

import re
names = re.findall(r'm_StatName:\s*(.*)', content)
print("Stat names found in database:")
for name in names:
    print(f"- {name}")
