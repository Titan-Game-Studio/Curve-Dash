import re

catalog_path = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame\Assets\Data\DTOS\MasterItemCatalog.asset"

print("Reading MasterItemCatalog.asset...")
with open(catalog_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find references to items inside MasterItemCatalog
# In Unity YAML, references are in the format: {fileID: ..., guid: ..., type: ...}
references = re.findall(r'guid:\s*([a-f0-9]{32})', content)
print(f"Total GUID references in catalog: {len(references)}")

# Let's map GUIDs to file paths by reading .meta files
meta_map = {}
import os
workspace_dir = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame"
assets_dir = os.path.join(workspace_dir, "Assets")

print("Scanning meta files to match GUIDs...")
for root, dirs, files in os.walk(assets_dir):
    for f in files:
        if f.endswith(".meta"):
            meta_path = os.path.join(root, f)
            with open(meta_path, 'r', encoding='utf-8', errors='ignore') as mf:
                m_cont = mf.read()
                guid_match = re.search(r'guid:\s*([a-f0-9]{32})', m_cont)
                if guid_match:
                    guid = guid_match.group(1)
                    asset_path = meta_path[:-5] # remove .meta
                    meta_map[guid] = os.path.relpath(asset_path, workspace_dir)

print("\nMatching catalog items:")
types_count = {}
for guid in references:
    if guid in meta_map:
        path = meta_map[guid]
        # Skip MasterItemCatalog itself
        if "MasterItemCatalog" in path:
            continue
        print(f"  Item in Catalog: {path}")
        # Count types based on directory name
        folder = path.split(os.sep)
        if len(folder) > 2:
            key = folder[-2]
            types_count[key] = types_count.get(key, 0) + 1
    else:
        print(f"  Unknown GUID: {guid}")

print("\nItem types in Catalog summary:")
for k, v in types_count.items():
    print(f"  {k}: {v}")
