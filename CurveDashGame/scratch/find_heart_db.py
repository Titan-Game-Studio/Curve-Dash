import os

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Let's see if we have "m_StatName: Heart"
print("Occurrences of 'Heart' in database:")
idx = 0
while True:
    idx = content.find("Heart", idx)
    if idx == -1:
        break
    start = content.rfind("\n", 0, idx) + 1
    end = content.find("\n", idx)
    print(f"Index {idx}: {content[start:end].strip()}")
    idx += 5
