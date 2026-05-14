import os

workspace_dir = r"d:\UNITY_PROJECTS\Curve-Dash\CurveDashGame"
assets_dir = os.path.join(workspace_dir, "Assets")

print("Scanning for .asset files...")
catalog_files = []
item_files = []

for root, dirs, files in os.walk(assets_dir):
    for f in files:
        if f.endswith(".asset"):
            path = os.path.join(root, f)
            rel = os.path.relpath(path, workspace_dir)
            if "catalog" in f.lower():
                catalog_files.append((f, rel))
            elif any(x in f.lower() for x in ["armor", "gem", "flask", "currency", "weapon", "shield", "bow", "sword", "maul"]):
                item_files.append((f, rel))

print(f"\n--- Catalog assets ({len(catalog_files)}) ---")
for f, r in catalog_files:
    print(f"  {f} -> {r}")

print(f"\n--- Item-related assets ({len(item_files)}) ---")
for f, r in item_files:
    print(f"  {f} -> {r}")
