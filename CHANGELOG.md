# Changelog

## 1.5.4 (2026-05-03)

### Changed
* Renamed the mod display name to CheatMenu+.
* Added clearer README attribution that CheatMenu+ is based on Sparroh's CheatMenu and Slidedrum's MenuMod2.

## 1.5.3 (2026-05-03)

### Fixed
* CheatMenu no longer toggles the game's menu camera renderer, preventing the world background from turning black.

## 1.5.2 (2026-05-03)

### Fixed
* Removed the menu backdrop darkening while keeping the invisible UI click catcher.
* Opening CheatMenu now locks firing until the menu is closed.

## 1.5.1 (2026-05-03)

### Fixed
* Opening CheatMenu now enables the game's menu input mode and locks player camera rotation while the menu is open.

## 1.5.0 (2026-05-03)

### Added
* Cosmetic Pickups now include a Custom pickup submenu for supported skin modifier fields.
* Custom cosmetic pickups can override numeric fields, toggles, and color channels such as chance, hue chance, max hue, trim colors, emissive colors, and VFX values.
* Custom cosmetic properties are persisted in a sidecar config file and rehydrated by instance ID plus cosmetic seed.

## 1.4.9 (2026-05-03)

### Added
* Added Cosmetic Pickups under UPGRADES, with random or typed-seed spawning for gear, character, and drop pod cosmetics.

## 1.4.8 (2026-05-03)

### Added
* Custom Stats upgrade pages now expose all editable stat inputs together and spawn one pickup with every filled-in stat applied.
* Custom stat pickups are persisted in a sidecar config file and rehydrated by instance ID on later launches.

## 1.4.7 (2026-05-03)

### Fixed
* Restored original upgrade properties after custom stat display/apply hooks so custom pickups do not leak their stats onto every matching inventory upgrade.

## 1.4.6 (2026-05-03)

### Fixed
* Custom stat overrides now swap the concrete upgrade property list used by gun upgrades, so spawned custom pickups can affect applied stats.

## 1.4.5 (2026-05-03)

### Fixed
* Removed abstract Harmony patches that could crash BepInEx while loading CheatMenu 1.4.4.

## 1.4.4 (2026-05-03)

### Changed
* Custom stat pickups now collect as the original game upgrade with per-instance stat overrides, so the game inventory can apply them normally.

## 1.4.3 (2026-05-03)

### Added
* Reintroduced Custom Stats as experimental mission pickups instead of direct inventory grants.

### Changed
* Custom stat upgrades now use in-memory cloned upgrades resolved per spawned instance, avoiding global custom upgrade registration.

## 1.4.2 (2026-05-03)

### Fixed
* Disabled the Custom Stats menu because runtime custom upgrade registration could pollute the game's own weapon/upgrade menu.

## 1.4.1 (2026-05-03)

### Fixed
* Custom Stats submenus now use unique internal IDs so duplicate weapon/upgrade display names cannot corrupt or duplicate the menu tree.

## 1.4.0 (2026-05-02)

### Added
* Added a Custom Stats submenu under UPGRADES with typed percentage input for supported upgrade modifiers.

### Changed
* Custom stat upgrades now use runtime cloned upgrade definitions instead of mutating the original shared upgrade asset.
* Removed the experimental Double Time x10 shortcut from Ammo & Abilities after confirming it can overwhelm the game.

## 1.3.2 (2026-05-02)

### Added
* Added an experimental Give Double Time x10 button under Ammo & Abilities.

## 1.3.1 (2026-05-02)

### Changed
* Toggle buttons now show explicit ON/OFF text in addition to changing color.

## 1.3.0 (2026-05-02)

### Added
* Added Ammo & Abilities, Movement, Teleport, Enemies, and Loot submenus.
* Added Infinite ammo, No cooldowns, ammo refill, and cooldown refresh controls.
* Added movement presets, saved-position teleport, crosshair teleport, enemy flee/intensity controls, and quick loot/resource actions.

## 1.2.0 (2026-05-02)

### Changed
* Reworked the menu into a classic GTA trainer-style layout with compact header, stacked rows, and translucent body.

## 1.1.4 (2026-05-02)

### Fixed
* Cleaned up sidebar title/version spacing and centered row text vertically.

## 1.1.3 (2026-05-02)

### Fixed
* Sidebar category rows now act as navigation buttons.

## 1.1.2 (2026-05-02)

### Changed
* Restyled the menu after the supplied reference with a left sidebar, search-style accent, and single-column settings list.

## 1.1.1 (2026-05-02)

### Changed
* Menu now takes a menu-camera lock while open so mouse input no longer clicks through into gameplay.
* Added Unlock everything to CHEATS and PROGRESSION.

## 1.1.0 (2026-05-02)

### Changed
* Rebuilt the menu UI with a centered panel, header, scrollable content, and cleaner button styling.
* Added an Infinite resources toggle to keep player resources topped up while enabled.

## 1.0.1 (2026-05-01)

### Changed
* Rebuilt and tested against Mycopunk 1.8.1 / Steam build 23053532.
* Changed the default menu keybind from backquote to Insert.

## 1.0.0 (2025-08-19)

### Features
* Initial release of CheatMenu mod
* In-game menu system accessible via backquote key
* Player cheats: God mode, fast sprint, super jump, air jump
* Spawn controls: Enemy spawning toggle, kill all, swarm spawning
* Mission system: Force modifiers, upgrades menu, progression tools
* Enemy management: Cleanup utilities for parts and collectables
* Profile validation: Requires non-default profile for functionality
* Harmony patches for real-time cheat application
