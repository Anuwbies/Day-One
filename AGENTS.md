# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity 6 project (`6000.0.63f1`). Core gameplay code lives in `Assets/Scripts`, grouped by feature such as `Player`, `Inventory`, `Craft`, `Shop`, `Structures`, and `World`. Scenes are in `Assets/Scenes`, reusable prefabs in `Assets/Prefab`, item data in `Assets/Item Data` and `Assets/Item Craft Data`, and runtime config in `Assets/StreamingAssets` (notably `blockchain_config.json`). Package dependencies are managed through `Packages/manifest.json`; engine and editor settings live in `ProjectSettings/`.

## Build, Test, and Development Commands
Open the project through Unity Hub and target the editor version in `ProjectSettings/ProjectVersion.txt`.

- `git clone <repo-url>`: clone the project locally.
- `Unity Hub -> Open -> Day-One`: import assets and generate local project files.
- `File -> Build Profiles`: create Windows or Android builds from the Unity Editor.
- `Window -> Package Manager`: verify package versions such as Input System, URP, and Thirdweb/Nethereum.

There is no checked-in CLI build script yet, so editor-driven workflows are the current standard.

## Coding Style & Naming Conventions
Follow the existing C# style in `Assets/Scripts`: 4-space indentation, one class per file, PascalCase for classes/methods/properties, and camelCase for private fields and local variables. Keep MonoBehaviour scripts focused on one gameplay responsibility and match file names to class names exactly, for example `PlayerMovement.cs` and `CampfireLogic.cs`. Preserve Unity `.meta` files for every moved or added asset.

## Testing Guidelines
`com.unity.test-framework` is installed, but there are no committed `Assets/Tests` suites yet. Add new automated coverage under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode` when changing game logic. Name test files after the system under test, such as `PlayerMovementTests.cs`. For scene-heavy changes, include a short manual test note in the PR describing scene, inputs, and expected result.

## Commit & Pull Request Guidelines
Recent commits use short subjects like `Added Leaf Bed` and `Campfire Placement`; keep the same imperative style, but be more specific than `Latest`. Preferred pattern: `Add campfire fuel drain` or `Fix inventory split UI`. Pull requests should include a concise summary, affected scenes/prefabs/scripts, linked issue or task, and screenshots or short clips for UI, scene, or animation changes.

## Security & Configuration Tips
Do not commit secrets, wallet keys, build outputs, `Library/`, or `Temp/`; `.gitignore` already excludes them. Treat `Assets/StreamingAssets/blockchain_config.json` as environment-specific and verify network IDs, client IDs, and contract addresses before merging.
