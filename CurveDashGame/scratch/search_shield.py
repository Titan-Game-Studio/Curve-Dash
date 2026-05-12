import os

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Let's search for "shield" case-insensitive
idx = 0
while True:
    idx = content.lower().find("shield", idx)
    if idx == -1:
        break
    # Print the line containing this match
    start = content.rfind("\n", 0, idx) + 1
    end = content.find("\n", idx)
    print(f"Match found at char {idx}: {content[start:end].strip()}")
    idx += 6
