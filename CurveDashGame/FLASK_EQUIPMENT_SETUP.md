# Flask Equipment System Setup Guide

## ✅ Hoàn Thành những gì

### 1. ✅ Flask Equipment Adapters Đã Được Tạo
- `Flask_Life_Flask_Small_Equipment_Adapter.asset`
- `Flask_Quicksilver_Flask_Equipment_Adapter.asset`
- `Flask_Granite_Flask_Equipment_Adapter.asset`

Các adapters này cho phép flask được quản lý bởi hệ thống equipment của Devion.

### 2. ✅ CurveDashEquipmentAdapter Đã Được Cập Nhật
- Thêm hỗ trợ cho `FlaskItemData`
- **Auto-assign regions** Flask 1, Flask 2, Flask 3 theo thứ tự (giống nhẫn!)

### 3. ✅ PlayerView Đã Được Cập Nhật
- Thêm method `UpdateBeltFlasksFromEquipment()` để quét equipment slots
- Flask ticking logic với actual health values
- Apply speed modifiers từ active flasks

## 🎯 Cấu Hình Cần Làm (3 Bước Đơn Giản)

### Bước 1: Tạo 3 Equipment Regions trong Devion ItemDatabase

Thêm 3 regions vào `ItemDatabase.equipments`:
```
- Flask 1  ← Thêm
- Flask 2  ← Thêm
- Flask 3  ← Thêm
```

(Giống như Ring Left, Ring Right cho nhẫn)

### Bước 2: Thêm Flask Equipment Adapters vào ItemDatabase

Mở **Devion ItemDatabase** và thêm 3 items mới:
1. `Flask_Life_Flask_Small_Equipment_Adapter`
2. `Flask_Quicksilver_Flask_Equipment_Adapter`
3. `Flask_Granite_Flask_Equipment_Adapter`

**Quan trọng:** Các adapters này sẽ TỰ ĐỘNG được gán vào Flask 1, Flask 2, Flask 3 regions theo thứ tự. Bạn không cần gán regions manually!

### Bước 3: Tạo 3 Equipment Slots trong Scene

Tạo 3 equipment slots trong UI của bạn, mỗi slot có:
- **EquipmentRegion** component
  - Region Name: "Flask 1" (hoặc "Flask 2", "Flask 3")

```
[Devion Equipment Container]
└─ [Equipment Slots]
   ├─ [Slot] → EquipmentRegion: "Flask 1"
   ├─ [Slot] → EquipmentRegion: "Flask 2"
   └─ [Slot] → EquipmentRegion: "Flask 3"
```

## 🔄 Cách Hoạt Động

### Trang bị Flask (Giống Nhẫn)
```
Người chơi kéo Flask #1 từ Inventory → Flask 1 slot
  ↓ Devion auto-detects Flask 1 region
  ↓ Flask #1 biến mất khỏi Inventory
  ↓ Flask #1 hiện ở Flask 1 slot

Kéo Flask #2 → Flask 2 slot (auto-fill tiếp)
Kéo Flask #3 → Flask 3 slot (auto-fill tiếp)
```

### Auto-Use Flask
1. **Mỗi frame:**
   - BeltFlaskService.Tick() được gọi
   - Kiểm tra health condition (< 60% → auto-use)
   - Healing apply → PlayerStatService.AddLife()
   - Speed modifiers apply → player.MovementSpeed

2. **Flask Settings:**
   - Life Flask: AutoUseCondition = 3 (WhenInjured)
   - Quicksilver: Speed +40%
   - Granite: Armor +20

## ✅ Kiểm Tra Hoạt Động

```
1. Chạy game (Play Mode)
2. Tìm Flask trong Inventory (hoặc add chúng)
3. Kéo Flask vào Flask 1 slot
   ✅ Flask biến mất khỏi Inventory
   ✅ Flask hiện ở Flask 1 slot
4. Kéo Flask khác vào Flask 2 slot
   ✅ Auto-fill tiếp (giống nhẫn)
5. Bị damage (health < 60%)
   ✅ Life Flask tự động dùng
   ✅ Console log: "Flask equipped via equipment: Life Flask Small"
```

## 📝 Notes

| Điểm | Chi Tiết |
|------|---------|
| **Regions** | Flask 1, Flask 2, Flask 3 (tự động assign) |
| **Auto-Assign** | Flask adapters TỰ ĐỘNG tìm region khả dụng |
| **Order** | Devion tự động fill theo thứ tự 1→2→3 |
| **Duplicate** | Khi trang bị, item được XÓA khỏi Inventory |
| **Auto-Use** | Hoạt động với Life Flask (health < 60%) |

## 🔧 Troubleshooting

| Vấn Đề | Nguyên Nhân | Giải Pháp |
|--------|-----------|---------|
| Flask không biến mất | Regions chưa add vào ItemDatabase | Thêm Flask 1, 2, 3 regions |
| Flask không hiện ở slot | ItemSlot chưa config đúng | Kiểm tra EquipmentRegion name match |
| Flask không auto-use | Belt chưa trang bị hoặc AutoUseCondition sai | Trang bị belt, check AutoUseCondition = 3 |
| Không thấy logs | PlayerView chưa attach EntityLinkView | Auto-attach khi game start |
