import os
import re

db_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Devion Games\Stat System\Examples\Database\StatsDatabase.asset"

with open(db_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

blocks = content.split("--- !u!")
print(f"Total YAML blocks in database: {len(blocks)}")

# Map fileID to block content
objects = {}
for block in blocks:
    if not block.strip():
        continue
    header_match = re.match(r"^(\d+)\s+&(\d+)", block)
    if header_match:
        utype = header_match.group(1)
        fid = int(header_match.group(2))
        objects[fid] = (utype, block)

print(f"Parsed {len(objects)} database sub-assets.")

# Find all stats in the database and print their m_StatName and fileID
for fid, (utype, block) in objects.items():
    if "m_StatName:" in block:
        name_match = re.search(r"m_StatName: (.+)", block)
        if name_match:
            stat_name = name_match.group(1).strip()
            print(f"Stat fileID={fid}: Name='{stat_name}'")
