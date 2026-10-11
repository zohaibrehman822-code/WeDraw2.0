# WeDraw — Project Memory (AI Context)

> Living document for AI assistants. Read this first when working on WeDraw.
> Companion files: `ai/TASKS.md` (work log). Last analyzed: 2026-10-07.

## What this project is

**WeDraw** — a kids' coloring/drawing mobile game (single-scene, panel-based UI).
- Company: **wikikreative** · Product: **WeDraw** · Version **1.0.3** (Android versionCode 4)
- Android bundle id: **`com.wecolors.kids.coloring.games`**
- No networking, no backend, no analytics, no runtime ads code (offline single-player app)

## Engine & environment

| Item | Value |
|---|---|
| Editor running | Unity **6000.5.2f1** (title bar), Android build target |
| AGENTS.md claims | 6000.1.7f1 — **stale**, actual editor is 6000.5.2f1 |
| Pipeline | URP 17.5.0 (`UniversalRenderPipelineGlobalSettings`) |
| Input | `activeInputHandler: 2` (**Both**) — drawing code uses **legacy** `Input.*`; `com.unity.inputsystem` 1.19 + `InputSystem_Actions.inputactions` also present |
| Scenes | `Assets/Scenes/SampleScene.unity` (the whole game) + `TestScene.unity` |
| Build settings | ⚠️ **SampleScene is DISABLED in EditorBuildSettings** (only scene in list) — a build right now has zero enabled scenes |
| Git repo | Yes. Latest: `7ad6a6a "Done with Almost all Setting also Version 9"` |

## Architecture: three drawing modes in one scene

Everything lives in **SampleScene**; modes are UI panels toggled at runtime (no scene loading).

### 1. Drawing Mode (trace-the-outline)
- Core: `Assets/DrawingManager.cs` (985 lines)
- Flow: `DrawingButtonManager.OnDrawingButtonClicked(i)` → `PlayerPrefs["DrawingImage"]=i` → `ReferenceImageManager.UpdateImageFromPrefs()` activates GameObject i → `DrawingManager` finds it via `GameObject.Find(name)`, draws on its `RawImage`
- **Boundary mask**: `PolygonCollider2D` baked once into `bool[]` (`CreateBoundaryMask`) — pixels outside outline can't be painted
- **5 brushes**: Solid, Crayon, Marker, Spray, Pencil (`BrushType` enum); eraser = white Solid
- **Undo**: stroke-level `Stack<List<PixelChange>>`, pixels snapshotted before first write; strokes <5 pixels ignored
- **Save**: binary `persistentDataPath/drawing_save_{index}.dat` (SAVE_VERSION 1) = full canvas pixels + entire undo stack; auto-saved after every committed stroke, on pause, on quit
- **Pixelation fix**: original source textures deep-cached in `static Dictionary<int,Texture2D> originalSourceTextures` so re-entry never re-downscales; mobile `textureScale = 2`
- **Input**: touch first, mouse only if no touch that frame (FIX #8); draw throttle 16ms

### 2. Mode 3 (free draw)
- Core: `Assets/Scripts/Mode3/Mode3Manager.cs` (904 lines) — near copy of DrawingManager but:
  - Pref key **`Mode3Image`**, save file **`mode3_save_{index}.dat`**, no boundary mask (free canvas), mouse-only input
  - Deferred first point (no phantom dot at stroke start), inspector-tunable brush multipliers
  - Object lookup via `switch` (same 40-name catalog, hard-coded twice)

### 3. Image Fill (tap-region coloring)
- Core: `Assets/Scripts/ImageFill/ImageFillManager.cs` (871 lines)
- Pref key **`FillImage`**, SAVE_VERSION **2**, markers: Solid, Glitter, Marker, Spray, Pencil, Eraser
- **Baked regions**: each `PolygonCollider2D` region's pixel indices baked once to `int[]` (`regionPixelIndices`) — tap fill is O(region) with zero physics calls at runtime (perf fix: was 360k OverlapPoint calls/tap)
- Extra: glitter sparkle system (grid-accelerated), base-coat alpha 0.28, `filledColliders` HashSet

### Shared image catalog (IMPORTANT gotcha)
- **40 images** (fruits → vegetables → sea creatures → animals) selected by **1-based index**
- Index → GameObject **name** mapping is **hard-coded in two places**:
  - `DrawingManager.GetObjectNameFromIndex()` (string[] of 40)
  - `Mode3Manager.GetObjectNameFromIndex()` (switch of 40)
- Names contain typos that MUST match scene GameObjects exactly: `Grappes`, `Gauva`, `Brocolli`/`brocolli` (casing differs between the two lists!), `Octupus`, `SmallFIsh`, `graph`, `EggPlant`
- Activation: `GameObject.Find(name)` → child `RawImage` (canvas) + `PolygonCollider2D` (boundary)
- `Level` folder also has `ReferenceImageManager` (class) in file `RefereneImageManager.cs` (file/class name mismatch — don't rename casually, scene refs are GUID-based)

## State & persistence

| Key | Where | Meaning |
|---|---|---|
| `DrawingImage` | PlayerPrefs | 1–40 selected image, Drawing Mode |
| `Mode3Image` | PlayerPrefs | 1–40 selected image, Mode 3 |
| `FillImage` | PlayerPrefs | 1–40 selected image, Fill Mode |
| `SelectedLevel` | PlayerPrefs | 1–5 (set by `LevelSelection`, currently no consumer logic found) |

- Canvas state: binary `.dat` files in `Application.persistentDataPath` (format: version, w, h, count, RGBA floats, then undo entries) — both mode managers serialize the **full undo stack**
- Bump `SAVE_VERSION` when the binary layout changes (mismatched saves are deleted on load)

## Script map (36 scripts)

```
Assets/
├─ DrawingManager.cs, ColorButton.cs, DrawingTarget.cs   ← Drawing Mode core (root-level)
├─ Scripts/
│  ├─ UI/         GameManager (loading→menu panel switch), DrawingButtonManager,
│  │              ObjectSwitcher (cyclic panel activator), LoadingBarManager,
│  │              RandomAnimatorActivator (class: RandomSequentialAnimator)
│  ├─ Mode3/      Mode3Manager, Mode3ButtonManager, Mode3ColorButton,
│  │              Mode3DrawingTarget, RefernceMode3ImageManager
│  ├─ ImageFill/  ImageFillManager, FillButtonManager, ColorButtonFillMode,
│  │              DrawingTargetFillMOde, ReferenceFillImageManager
│  ├─ Level/      LevelSelection, RefereneImageManager (class ReferenceImageManager)
│  ├─ Sounds/     DrawingPanelSOundManager, Mode3SoundManager, FillModeSoundManager,
│  │              BackgroundSoundManager, PanelFillSound  (per-mode audio)
│  └─ DrawingMode/ First-time tutorials: FirstTimeDrawingChecker/ UI classes,
│                 TutorialPanelController (IPointerClickHandler)
├─ ScreenshotCamera.cs, QuitManager.cs, ScrollViewAutoResize.cs, LoopingScrollView.cs
└─ UGUI Anchor Automatically/Editor/uGUITools.cs (editor tool)
```

## Packages (Packages/manifest.json)

URP 17.5, InputSystem 1.19, 2D suite (animation/sprite/tilemap/psd/aseprite/spriteshape), ugui 2.5, visualscripting, test-framework, timeline, collab-proxy, device-simulator, **`com.coplaydev.unity-mcp` v10.3.0** (added 2026-10-07, git URL), EDM4U (`ExternalDependencyManager`), Google-Mobile-Ads **plugin remnants only** (iOS headers `Assets/Plugins/iOS/GADU*`, stale `GoogleMobileAds.Editor.csproj`) — no runtime ads code anywhere.

## AI tooling (set up 2026-10-07)

- `opencode.json` (project): MCP servers **context7** (docs) + **playwright** (browser; Chromium 155 installed)
- Global `~/.config/opencode/opencode.json`: **unityMCP** (remote `http://127.0.0.1:8080/mcp`) — written/managed by the Unity plugin; **the Unity Editor itself spawns the HTTP server** (port 8080, `--unity-instance-token`), so it only works while Unity is open with this project
- A duplicate project-level `unity` stdio entry was **removed** (idle, spawned extra processes)
- MCP for Unity package: `Window → MCP for Unity` for bridge status; tools available to AI when connected
- `ai/MEMORY.md` = this file · `ai/TASKS.md` = work log — **append new findings/tasks there**

## Known issues / tech debt (as of 2026-10-07)

1. ⚠️ SampleScene disabled in build settings → empty build
2. AGENTS.md stale (editor version, active GameObject "AdsManager" **does not exist** in any scene — grep found nothing)
3. Dirty ProjectSettings not committed: `EditorSettings.asset` (serializedVersion 14→16, EnterPlayModeOptions 0→1, new Unity 6.5 fields) + `EditorBuildSettings.asset` (scene disabled) — clarify with user before committing
4. Duplicate hard-coded 40-image catalogs (drift risk — `brocolli` vs `Brocolli` already differs)
5. File/class name mismatches (`RefereneImageManager.cs`, `RefernceMode3ImageManager`, `DrawingTargetFillMOde`, `DrawingPanelSOundManager`) — renames must keep GUIDs intact
6. `LevelSelection` sets `SelectedLevel` but nothing reads it
7. Mode3Manager duplicates DrawingManager wholesale (~900 lines) — refactor candidate if touching brush logic
8. 2 solutions on disk: `WeDraw.sln`/`WeDraw.slnx` + stale `DrawingBook.sln`
9. Heavy `Debug.Log` logging in hot paths (per-stroke save/load) — noisy in release builds

## Conventions observed in this codebase

- Comments use `FIX #N` markers and `=== SECTION ===` banners; methods are grouped by section
- Public methods are UnityEvent-friendly (e.g. `SetBrushToSolid()`, `OnBrushSizeDragStart()`) — wired via UGUI buttons
- Managers are plain MonoBehaviours, no singletons/events — cross-object calls via inspector refs + `FindObjectOfType` fallbacks
- Sound managers per mode, gated by `allowBrushingSound` flag
- Screenshots: desktop → Desktop folder, Android → `persistentDataPath` + MediaScanner
