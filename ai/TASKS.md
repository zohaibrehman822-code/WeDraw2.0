# WeDraw — AI Task Log

> Append-only work log. Newest entries at the bottom of each date.
> Companion: `ai/MEMORY.md` (architecture / project knowledge).

---

## 2026-10-07 — Session 1: MCP setup + full project analysis

### 1. opencode MCP servers (goal: Unity MCP + Context7 + Playwright)

- **Context7**: created project `opencode.json` with `npx -y @upstash/context7-mcp` → live-verified (handshake OK, v4.2.0).
- **Playwright**: added `npx -y @playwright/mcp@latest` → 25 tools; installed matching browser
  `npx -y playwright@1.64.0-alpha-1790635538000 install chromium` (Chromium 155, required by `@playwright/mcp@0.0.83`).
- **Unity MCP**:
  - Discovered the Unity plugin writes its own entry into the **global** config `C:\Users\Zohaib\.config\opencode\opencode.json` as `unityMCP` (remote `http://127.0.0.1:8080/mcp`, spawned by the Unity Editor itself with `--unity-instance-token`).
  - Initially also created a project-level `unity` stdio entry → caused a **duplicate**: idle process tree (~5 extra `UnityMCP`/cmd processes, 0 instances) while the remote was the only live bridge.
  - **Fix**: removed the redundant `unity` entry from `opencode.json`; kept `unityMCP` remote only. Verified live instance `WeDraw@09310d767b9a1157` (Unity 6000.5.2f1) via `mcpforunity://instances` + scene query (`SampleScene`, 15 roots).
  - Note: opencode loads config at startup — the `unity` stdio entry disappears fully after next opencode restart.
- Installed the editor-side package: added `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0"` to `Packages/manifest.json` (tag verified with `git ls-remote`), resolved by the editor into `Library/PackageCache`.
- All three servers confirmed working with real round-trips (JSON-RPC handshake, resource reads, tool calls).

### 2. Full project analysis

Read across the repo (36 C# scripts, manifest, settings, scenes, git history) — conclusions captured in **`ai/MEMORY.md`**. Highlights:
- Single-scene kids coloring app; 3 independent drawing systems (Drawing / Mode3 free-draw / Image-Fill) sharing a hard-coded 40-image catalog and PlayerPrefs keys (`DrawingImage`, `Mode3Image`, `FillImage`, `SelectedLevel`).
- CPU-pixel rendering with baked boundary masks, baked fill regions, stroke-level undo, binary saves incl. full undo stack.
- No networking/ads/backend code (AdsManager note in AGENTS.md is stale).

### 3. Deliverables created

| File | Purpose |
|---|---|
| `opencode.json` (project) | MCP: context7 + playwright |
| `ai/MEMORY.md` | Project architecture & AI context memory |
| `ai/TASKS.md` | This task log |
| `Packages/manifest.json` (+lock) | Added `com.coplaydev.unity-mcp` v10.3.0 |

### 4. Observations left for the user (not acted on)

- ⚠️ `SampleScene` is **disabled** in EditorBuildSettings → builds would contain no scenes.
- Dirty, uncommitted: `ProjectSettings/EditorBuildSettings.asset`, `EditorSettings.asset` (Unity 6.5 auto-upgrade: serializedVersion 14→16, EnterPlayModeOptions 0→1) — **ask user before committing**.
- Stale `AGENTS.md` (editor version 6000.1.7f1; "AdsManager" doesn't exist).
- `LevelSelection` writes `SelectedLevel` but no script reads it.

---

*How to use: when finishing a task, append a dated section here with: what was done, files touched, verification result, open questions.*
