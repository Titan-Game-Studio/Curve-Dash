# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Curve-Dash is an action RPG dungeon crawler (Path of Exile-inspired) built in **Unity 6000.3.11f1**, targeting mobile and PC. Core namespace: `STG.CurveDash`.

## Architecture

### ECS (Leopotam.EcsLite)

All real-time gameplay lives in ECS systems. `EcsStartup.cs` owns the `EcsWorld` and `EcsSystems` and ticks in LateUpdate.

- **Components** (`Assets/Scripts/Components/`) — pure data structs: `PlayerComponent`, `PlayerStatComponent`, `PlayerCombatComponent`, `EnemyComponent`, `GameStateComponent`, `ViewLinkComponent`, event components (`PlayerHitCrystalEvent`, `EnemyDeadEvent`, etc.)
- **Systems** (`Assets/Scripts/Systems/`) — one concern per system: `CameraFollowSystem`, `CombatSystem`, `BlockSystem` (also spawns monsters), `FallingSystem`, `VfxSystem`, `DeleteEventsSystem`, etc.
- **Event pattern**: create a temporary entity with an event component, process it in the same frame, delete it via `DeleteEventsSystem`.
- **View bridging**: `ViewLinkComponent` holds GameObject/Transform refs; `EntityLinkView` (MonoBehaviour) stores the packed entity so Unity callbacks can find ECS entities.

### Dependency Injection (Zenject / Extenject)

Services and settings are bound via Zenject installers. `GameSettingsInstaller` binds `GameSettings` and `AudioSettings`. Key injectables: `DataManager`, `ShopService`, `AssetManager`, `VfxPoolManager`, `SmartEquipService`.

### Settings-Driven Design

`GameSettings` ScriptableObject holds gameplay constants. Catalogs (`ItemCatalog`, `MonsterCatalog`, `WeaponDataCatalog`, `ShopItemCatalog`, `AudioMappingCatalog`) are ScriptableObjects referenced from settings.

## Equipment & Item System

The inventory UI is powered by the **Devion Games Inventory System** (third-party). Custom code bridges it to game logic.

### Key bridge classes
- `CurveDashEquipmentAdapter` — Devion ↔ game data sync; listens to Devion slot events
- `EquipmentResolver` (`Assets/Scripts/Systems/Equipment/EquipmentResolver.cs`) — resolves which visual slots (LeftHand/RightHand) each item occupies
- `SmartEquipService` — auto-fill and partner-equipping logic (e.g. bow auto-equips arrows)

### Slot assignment rules (EquipmentResolver)
- `BowData` → `{LeftHand=bow, RightHand=arrow}`
- `TwoHandedWeaponData` → both hands
- `OneHandedWeaponData` → right hand primary, supports dual-wield
- `OffHandData.Arrow` → must pair with a bow
- `OffHandData.Shield` → left hand; incompatible with 2H/Bow

### Item hierarchy
`ItemData` → `WeaponData` → `BowData` | `OneHandedWeaponData` | `TwoHandedWeaponData` | `OffHandData`
Also: `ArmorItemData`, `GemItemData`, `CurrencyItemData`.

### Devion integration caveat
Devion fires slot-change events synchronously during its own update loop. Avoid writing back to Devion slots inside those event handlers — use `pendingAutoEquippedPartner` deferred pattern already established in `SmartEquipService`.

## Data & Persistence

- `DataManager` — JSON-serializes `UserData` to PlayerPrefs
- `UserData` — coins, equipped character, equipment dictionary, shop unlocks, cloud sync timestamp
- `CloudSaveManager` — Unity Cloud Save integration

## Game State Flow

`GameStateComponent.State` enum: `Title → Playing → GameOver → GameEnd`

Gameplay loop: player moves (input → `PlayerComponent.Direction`) → camera/terrain follow → monsters spawn ahead every 2.5s at ~40m → collision events → combat (attack timer + cooldown) → loot drops → equipment updates trigger stat recalc.

## Key Directories

```
Assets/Scripts/
  Components/         ECS data components
  Systems/            ECS systems (Equipment/ subfolder for equipment logic)
  Views/              MonoBehaviour view layer
  Services/           Business logic (ShopService, SmartEquipService, etc.)
  Settings/           ScriptableObject catalogs & GameSettings
  ScriptableObjects/  ItemData, WeaponData, MonsterData, ability definitions
  Models/             UserData, WeaponInstance, ItemStat, StatModifier
  UI/                 UI controllers & elements
  Managers/           DataManager, CloudSaveManager, VfxPoolManager
  Editor/             Editor tools and editor-mode tests
  Test/               Runtime tests
Assets/Scenes/
  MainGame.unity      Primary gameplay scene
  TestScene.unity
```

## Core Dependencies

| Package | Purpose |
|---------|---------|
| `com.leopotam.ecslite` | ECS framework |
| `com.svermeulen.extenject` | Zenject DI |
| `com.unity.addressables` | Asset loading |
| `com.unity.inputsystem` | Input |
| `com.unity.services.cloudsave` | Cloud persistence |
| `com.unity.render-pipelines.universal` | URP |
| Devion Games Inventory System | Inventory/equipment UI (third-party, in Assets/) |

## Development Notes

- **No build CLI** — open the project in Unity Editor (6000.3.11f1) and use the Unity Editor build pipeline.
- **Tests** live in `Assets/Scripts/Editor/` (edit-mode) and `Assets/Scripts/Test/` (play-mode). Run via Unity Test Runner (`Window > General > Test Runner`).
- Equipment data assets live in `Assets/Data/DTOS/Weapons/` as ScriptableObject `.asset` files; edit them in the Inspector or with custom editor tools in `Assets/Scripts/Editor/`.
- The Devion ItemDatabase for equipment is `CurveDash_Vaal_Reliquary` — equipment regions are `MainHand` and `OffHand`.
- VFX and monster GameObjects use Zenject pools (`VfxPoolManager`) — do not Destroy them directly; return them to the pool.
