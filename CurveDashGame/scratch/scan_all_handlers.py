import os
import re

scene_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Scenes\MainGame.unity"

with open(scene_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

blocks = content.split("--- !u!")
print(f"Total YAML blocks: {len(blocks)}")

# Let's map fileID to its parsed details
objects = {}
for block in blocks:
    if not block.strip():
        continue
    header_match = re.match(r"^(\d+)\s+&(\d+)", block)
    if header_match:
        utype = header_match.group(1)
        fid = int(header_match.group(2))
        objects[fid] = (utype, block)

# StatsHandler script guid is the one from StatsHandler.cs.meta
# Let's see if we can find its guid. If not, let's search for m_HandlerName in MonoBehaviour blocks.
handlers = []
for fid, (utype, block) in objects.items():
    if utype == "114" and "m_HandlerName:" in block:
        handlers.append((fid, block))

print(f"Found {len(handlers)} StatsHandler components in scene:")

for fid, block in handlers:
    go_id = None
    go_match = re.search(r"m_GameObject: {fileID: (\d+)}", block)
    if go_match:
        go_id = int(go_match.group(1))
    
    hname = ""
    hname_match = re.search(r"m_HandlerName: (.+)", block)
    if hname_match:
        hname = hname_match.group(1).strip()
        
    go_name = "Unknown"
    if go_id and go_id in objects:
        _, go_block = objects[go_id]
        name_match = re.search(r"m_Name: (.+)", go_block)
        if name_match:
            go_name = name_match.group(1).strip()
            
    print(f"- StatsHandler {fid} on GameObject '{go_name}' (ID {go_id}): m_HandlerName='{hname}'")
