# Curve-Dash: Bow+Arrow Equip Bug — Debug Handoff

## Tổng quan dự án

- **Engine**: Unity, C#, namespace `STG.CurveDash`
- **DI**: Zenject
- **Inventory UI**: Devion Games Inventory System (third-party asset)
- **Custom database asset**: `CurveDash_Vaal_Reliquary` (ItemDatabase)
- **Branch**: `develop`

---

## Kiến trúc Equipment (quan trọng)

### Luồng equip item

1. Người chơi kéo item vào Equipment UI (Devion)
2. Devion fires `ItemContainer.OnAddItem` → `PlayerView.OnDevionEquipmentAdded(item, slot)`
3. `OnDevionEquipmentAdded` gọi `Equip(equippable)` → `SmartEquipService.SmartEquip()`
4. `SmartEquip` trả về `LoadoutAssignment { LeftHand, RightHand }`
5. `PlayerView` set `_leftHandItem` / `_rightHandItem`
6. `RefreshWeaponVisuals()` spawn 3D model lên tay nhân vật

### Slot tay trong Devion

- **Right Hand** (Main Weapon) = `EquipmentRegion "MainHand"` trong `CurveDash_Vaal_Reliquary`
- **Left Hand** (Off-Hand) = `EquipmentRegion "OffHand"` trong `CurveDash_Vaal_Reliquary`
- `GetSlotByRegionName("Right")` / `GetSlotByRegionName("Left")` — tìm slot theo substring (Method D dùng GO name)

### Quy tắc tay cho từng weapon type

| Weapon Type | `_leftHandItem` | `_rightHandItem` |
|---|---|---|
| `BowData` | Bow | Arrow (`OffHandData.Arrow`) |
| `TwoHandedWeaponData` | Hammer/GreatSword | (mirror, same ref) |
| `OneHandedWeaponData` | (empty hoặc 1H/Shield) | Sword |
| `OffHandData.Arrow` | — | Arrow |
| `OffHandData.Shield` | Shield | — |

---

## Vấn đề đã fix (Session trước + Session này)

---

### Bug 1: Weapon bị gán region sai → Devion đặt vào slot Head

**File**: `Assets/Scripts/ScriptableObjects/CurveDashEquipmentAdapter.cs`

**Root Cause**:  
`SyncDatabaseReferences()` tìm region bằng keyword `"Right"` / `"Left"` nhưng database `CurveDash_Vaal_Reliquary` đặt tên region là `"MainHand"` và `"OffHand"`. Không match → `Region = []` (empty) → Devion fallback về slot[0] = Head → Helmet bị đẩy ra.

**Fix đã apply** (lines 276–294 trong `CurveDashEquipmentAdapter.cs`):

```csharp
if (m_OriginalEquipmentData is WeaponData || m_OriginalEquipmentData is OffHandData)
{
    var rightRegion = db.equipments.Find(r =>
        r.Name.IndexOf("MainHand", StringComparison.OrdinalIgnoreCase) >= 0 ||
        r.Name.IndexOf("Main Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
        r.Name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0);
    var leftRegion = db.equipments.Find(r =>
        r.Name.IndexOf("OffHand", StringComparison.OrdinalIgnoreCase) >= 0 ||
        r.Name.IndexOf("Off Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
        r.Name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0);
    
    this.Region = new List<EquipmentRegion>();
    if (rightRegion != null) this.Region.Add(rightRegion);
    if (leftRegion != null) this.Region.Add(leftRegion);
    return;
}
```

**Status**: ✅ Fixed

---

### Bug 2: Equip Bow → Arrow bị đặt ở CẢ 2 tay trong UI

**File**: `Assets/Scripts/Views/PlayerView.cs`

**Symptom**: Equip cung (BowData) → Devion UI hiển thị Arrow icon ở cả Right Hand lẫn Left Hand. Đúng ra: Right Hand = Bow icon, Left Hand = Arrow icon.

#### Root Cause A: Devion auto-fill bow vào cả 2 slot

Bow có `Region = [MainHand, OffHand]` → Devion cố fill CÙNG một item instance vào **cả 2 slot** → bắn 2 event `OnDevionEquipmentAdded` (một cho rightSlot, một cho leftSlot).

**rightSlot event** (xử lý đúng):
1. Nhận bow vào right
2. Tìm arrow trong inventory → `ReplaceItem(leftSlot, arrowItem)` ← Arrow vào leftSlot ✓
3. Gọi `Equip(bow)` → `SmartEquip` trả về `{Left=bow, Right=???}`

**leftSlot event** (xảy ra SAU, bow đã bị thay bằng arrow):
- Code cũ không kiểm tra — thấy `BowData` trong leftSlot event → cố move bow sang right → nhưng `slot.ObservedItem` lúc này đã là **arrow** (không phải bow) → gây chaos.

#### Root Cause B: Arrow event bị block bởi `_isUpdatingEquipment` guard

Khi rightSlot handler gọi `_equipmentContainer.ReplaceItem(leftSlot, arrowItem)`, Devion fires `OnDevionEquipmentAdded(arrow, leftSlot)` **ngay lập tức** (synchronous) nhưng `_isUpdatingEquipment = true` → event bị **blocked/lost**.

Sau đó `Equip(bow)` → `SmartEquip.AutoFillEmptyPartnerSlot` tìm arrow trong **inventory** (không có, vì đã move sang equipment) → trả về `{Left=bow, Right=null}` → `_rightHandItem = null` → không có arrow 3D model trên tay.

---

#### Fix đã apply (3 thay đổi trong `PlayerView.cs`)

**Fix 2a — `pendingAutoEquippedPartner`** (line 467):
```csharp
var itemData = adapter.OriginalEquipmentData;
EquippableData pendingAutoEquippedPartner = null; // set when bow auto-equips arrows
```

**Fix 2b — Lưu arrow ref khi auto-equip** (line 557–558):
```csharp
_equipmentContainer.ReplaceItem(leftSlot.Index, arrowItem);
Debug.Log("<color=lime>[PlayerView] Great Bow equipped. Automatically equipped Arrows from Inventory in Left Hand.</color>");
// Arrow event was blocked by guard — schedule Equip(arrow) after Equip(bow)
if (arrowItem is CurveDashEquipmentAdapter arrowAdapterRef && arrowAdapterRef.OriginalEquipmentData is EquippableData arrowEquip)
    pendingAutoEquippedPartner = arrowEquip;
```

**Fix 2c — Gọi `Equip(arrow)` sau `Equip(bow)`** (lines 747–753):
```csharp
Equip(equippable);
// If bow auto-equipped arrows (event was blocked by guard), sync game state now
if (pendingAutoEquippedPartner != null)
{
    Debug.Log($"<color=lime>[PlayerView] Bow auto-arrow partner: calling Equip('{pendingAutoEquippedPartner.name}') to set _rightHandItem</color>");
    Equip(pendingAutoEquippedPartner);
}
```

**Fix 2d — Stale leftSlot bow event guard** (lines 610–640):
```csharp
if (itemData is TwoHandedWeaponData || itemData is BowData)
{
    // Guard: by the time this leftSlot event fires, rightSlot handler already replaced
    // leftSlot with arrows. If slot no longer contains the bow, skip.
    if (slot.ObservedItem != item)
    {
        Debug.Log($"<color=yellow>[PlayerView] BowData/2H leftSlot event ignored — slot now has '{slot.ObservedItem?.Name ?? "empty"}' (stale). Skipping.</color>");
        goto skipLeftSlotBowMove;
    }
    _equipmentContainer.RemoveItem(slot.Index);
    // ... move to right ...
    return;
}
skipLeftSlotBowMove:
```

**Status**: ✅ Fixed (code đã commit, chưa test Play Mode)

---

## Pending Tasks — Gemini cần làm

### 1. Test trong Unity Play Mode

Bước test:
1. Start game
2. Đảm bảo có Arrow trong Inventory
3. Equip Bow vào Right Hand slot
4. Kiểm tra:
   - [ ] Devion UI: Right Hand slot = Bow icon
   - [ ] Devion UI: Left Hand slot = Arrow icon  
   - [ ] Character 3D: Left hand bone có bow model
   - [ ] Character 3D: Right hand bone có arrow model
   - [ ] Log: `[REGION-DIAG]` xác nhận DB có "MainHand"/"OffHand"
   - [ ] Log: `Bow auto-arrow partner: calling Equip(arrow)` xuất hiện
   - [ ] Không có "arrows in both hands" bug nữa

### 2. Verify Helmet không bị biến mất

Equip weapon rồi check:
- [ ] Helmet icon vẫn còn trong Head slot của Devion UI
- [ ] `[REGION-DIAG]` log: weapon được assign đúng region (MainHand/OffHand)

### 3. Cleanup debug logs

Sau khi confirm tất cả hoạt động đúng, xoá các log verbose:
- `[REGION-DIAG]` logs trong `CurveDashEquipmentAdapter.SyncDatabaseReferences()`
- `[DIAG]` logs chi tiết trong `PlayerView.OnDevionEquipmentAdded`
- `[DB-DIAG]` logs trong `SyncDatabaseReferences()`

### 4. Potential Issue: `SmartEquipService.AutoFillEmptyPartnerSlot`

**Đây có thể vẫn gây bug nếu:**
- Người chơi equip arrow TRỰC TIẾP (không qua bow auto-fill)
- `AutoFillEmptyPartnerSlot` của `BowData` gọi `_scanner.FindFirstArrow()` — scans **inventory only**, không scan equipment container

**Nếu vẫn bug**, check file:  
`Assets/Scripts/Systems/Equipment/SmartEquipService.cs`  

Tìm:
```csharp
case BowData when assignment.RightHand == null:
    assignment.RightHand = _scanner.FindFirstArrow();
    break;
```

Và file `Assets/Scripts/Systems/Equipment/PlayerInventoryScanner.cs` — method `FindFirstArrow()`.

Nếu scanner chỉ scan inventory thì cần thêm logic scan equipment container.

---

## Files liên quan

| File | Vai trò |
|------|---------|
| `Assets/Scripts/Views/PlayerView.cs` | Main equip event handler, `_leftHandItem`/`_rightHandItem`, `Equip()`, `Unequip()` |
| `Assets/Scripts/ScriptableObjects/CurveDashEquipmentAdapter.cs` | Devion adapter, `SyncData()`, `SyncDatabaseReferences()` |
| `Assets/Scripts/Systems/Equipment/EquipmentResolver.cs` | Rule engine: `BowData` → `{Left=bow, Right=arrow}` |
| `Assets/Scripts/Systems/Equipment/SmartEquipService.cs` | `SmartEquip()`, `AutoFillEmptyPartnerSlot()` |
| `Assets/Scripts/Systems/Equipment/PlayerInventoryScanner.cs` | `FindFirstArrow()` — scans inventory |

---

## Key Classes & Enums

```csharp
// Weapon types
class BowData : WeaponData { }
class TwoHandedWeaponData : WeaponData { }
class OneHandedWeaponData : WeaponData { }
class OffHandData : EquippableData { OffHandType SubType; } // Arrow | Shield
enum OffHandType { Arrow, Shield }

// Devion adapter
class CurveDashEquipmentAdapter : DevionGames.InventorySystem.EquipmentItem {
    ItemData OriginalEquipmentData; // link về game data gốc
    void SyncData(); // copy name/icon/prefab/region từ original → devion
}

// Assignment result
struct LoadoutAssignment {
    EquippableData LeftHand;   // Bow, TwoHanded, Shield
    EquippableData RightHand;  // OneHanded Sword, Arrow
    bool IsValid;
    string InvalidReason;
}
```

---

## Luồng Event khi Equip Bow (sau fix)

```
User drags Bow → Right Hand slot
  └─ Devion: OnAddItem(bow, rightSlot)
       └─ _isUpdatingEquipment = true
       └─ OnDevionEquipmentAdded(bow, rightSlot)
            ├─ BowData branch: find arrow in inventory
            ├─ ReplaceItem(leftSlot, arrow)   ← Devion fires OnAddItem(arrow, leftSlot)
            │    └─ BLOCKED by guard → lost
            ├─ pendingAutoEquippedPartner = arrowEquip  ← [Fix 2b]
            ├─ Equip(bow) → SmartEquip → {Left=bow, Right=null}
            │    └─ _leftHandItem = bow, _rightHandItem = null
            └─ pendingAutoEquippedPartner != null → Equip(arrow)  ← [Fix 2c]
                 └─ SmartEquip(arrow) với left=bow → {Left=bow, Right=arrow}
                      └─ _leftHandItem = bow, _rightHandItem = arrow  ✓

  Devion also fires OnAddItem(bow, leftSlot)  ← stale auto-fill
    └─ slot.ObservedItem = arrow (NOT bow) → goto skipLeftSlotBowMove  ← [Fix 2d]
```

---

## Ghi chú kỹ thuật

- **`_isUpdatingEquipment` guard**: Prevents re-entrant Devion events. Events fired INSIDE the guard are permanently LOST (not queued). This is why `pendingAutoEquippedPartner` pattern was needed.
- **`goto skipLeftSlotBowMove`**: Unusual but necessary — C# doesn't have labeled `break` for if statements. The goto skips the "move to right" logic but falls through to arrow/shield checks below.
- **`GetSlotByRegionName("Right")`**: Uses 4 search methods (GetComponents, GetComponentsInParent, restrictions list, GO name substring). Method D (GO name) works because the Equipment UI GameObjects are named "Right Hand" and "Left Hand".
- **Devion region auto-fill**: When `EquipmentItem.Region = [MainHand, OffHand]`, Devion attempts to fill ALL matching slots with the same item instance. This causes 2 `OnAddItem` events per equip for weapons.
