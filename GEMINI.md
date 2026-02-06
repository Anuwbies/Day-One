# GEMINI.md - Project Context: Day-One Survival

## Project Overview
**Day-One** is a 2D top-down survival game prototype built with Unity. The project focuses on core survival mechanics including resource gathering, inventory management, crafting, and world interaction within a procedurally populated environment.

### Main Technologies
- **Game Engine:** Unity (Version 2022.3+ recommended)
- **Language:** C#
- **Rendering:** Universal Render Pipeline (URP) 2D
- **Systems:** 
  - Unity Tilemap for environment design.
  - Unity Input System (Mixed usage with legacy Input).
  - ScriptableObjects for data-driven item and crafting systems.

## Architecture & File Structure
The project follows a feature-based organization within `Assets/Scripts/`:

- `Assets/Scripts/Player/`: Handles movement (`PlayerMovement`), interaction (`PlayerPickup`), and local inventory logic (`PlayerInventory`).
- `Assets/Scripts/Inventory/`: UI-heavy scripts managing the grid, drag-and-drop (`InventorySlotDragDrop`), and context menus.
- `Assets/Scripts/Items/`: Contains `ItemData.cs` (ScriptableObject definition) and `Item.cs` (world object representation).
- `Assets/Scripts/World Generator/`: Logic for populating tilemaps with resources (e.g., `ForestAreaGenerator`).
- `Assets/Scripts/For All/`: Shared utilities and generic systems:
    - `YSorter.cs`: Handles 2D depth sorting based on Y-position.
    - `DamageTarget.cs` / `EnemyHealth.cs`: Generic health and damage interaction system.
    - `DropLoot.cs`: Handles item spawning upon object destruction.
- `Assets/Scripts/Day & Night/`: Manages time-of-day lighting shifts using URP.

## Building and Running
- **Setup:** Open the project folder in Unity Hub. Ensure the Universal Render Pipeline (URP) packages are correctly resolved.
- **Main Scene:** `Assets/Scenes/1st Game Scene.unity` is the primary gameplay entry point.
- **Menu Scenes:** Located in `Assets/Scenes/`, including `Main Menu.unity` and `Settings.unity`.
- **Controls:** 
    - Movement: WASD / Arrow Keys.
    - Interaction: Likely Mouse-based for UI and proximity-based for pickups.

## Development Conventions
- **Data Driven:** Always use ScriptableObjects (found in `Assets/Item Data/`) to define new items or recipes.
- **Editor UX:** Use `[Header]` and `[Tooltip]` attributes for serialized fields to maintain a clean Inspector.
- **Performance:** World objects use `YSorter` for visual depth; ensure new sprites are configured for this if they are meant to be traversed.
- **Namespaces:** Currently, the project does not heavily use namespaces; follow this pattern for consistency unless refactoring.
- **Formatting:** Standard C# PascalCase for methods and public fields, camelCase for private fields.

## Key Files for Reference
- `Assets/Scripts/Items/ItemData.cs`: The core definition for all items.
- `Assets/Scripts/Player/PlayerMovement.cs`: Reference for player control logic.
- `Assets/Scripts/World Generator/ForestGenerator.cs`: Reference for how trees and resources are spawned.
- `Assets/Scripts/Inventory/InventoryUI.cs`: Central hub for inventory visual management.
