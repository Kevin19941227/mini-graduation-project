Even if Codex is launched with full access, treat this project as workspace-limited.
Do not read, write, delete, move, or inspect files outside this repository unless I explicitly request it in the current chat.

# Codex Rules for this Unity Project

This is a Unity project. Keep changes small and safe.

## Allowed areas

Codex may inspect and modify only these folders unless I explicitly say otherwise:

- Assets/TestingScript/
- Assets/Network/
- Assets/BaseData/
- Assets/DataScript/
- Assets/test(delete)/

## Forbidden areas

Codex must not modify:

- Assets/plugin/
- Assets/Plugins/
- Packages/
- ProjectSettings/
- Library/
- Logs/
- UserSettings/
- .vscode/
- .vs/
- obj/
- TextMesh Pro/
- Any .csproj files
- Any .sln files
- Any .meta files

## Unity rules

- Do not edit third-party plugin code.
- Do not delete, rename, or regenerate Unity .meta files.
- Do not modify scenes or prefabs unless I explicitly ask.
- Do not rename serialized fields unless you explain the migration risk first.
- Do not modify Package Manager settings unless I explicitly ask.
- Prefer changing one script at a time.
- If a change affects Mirror networking, explain whether it is server-side, client-side, or UI-only.

## Before editing

Before modifying files, Codex must list:

1. Which files it will inspect.
2. Which files it will modify.
3. Why those files are needed.

If the task can be solved by explanation only, do not edit files.

# PHOBOS Development Rules

These rules are based on `C:\Users\fhcsij\Downloads\恐懼工廠_AI助手指令 (1).md`. Follow them for this project unless the user explicitly says otherwise.

## Project Stack

- Engine: Unity with URP, possibly UTS3 toon shader.
- Networking: Mirror. Use KCP Transport for local testing and FizzySteamworks Steam P2P for release/Steam flow.
- Movement: KinematicCharacterController (KCC).
- Input: Unity Input System.
- Camera: Cinemachine.
- Multi-client local testing: ParrelSync.
- State architecture: layered state machine, `IState` contracts, and ScriptableObject data where appropriate.
- Physics authority: server-side authority; gate authoritative behavior with `isServer` / server methods.

## Known Hazards

- Mirror + KCC: non-local players must disable `KinematicCharacterMotor` in `Start()` so KCC and NetworkTransform do not fight for transform ownership.
- SyncVar hooks only run when values change. For late-joining clients, explicitly apply current state in `OnStartClient()` where needed.
- Transport switching must set both `Transport.active` and the NetworkManager `transport` field.
- NetworkTransform on physics objects should use world-space synchronization when local space would drift.
- KCC idle/null states should keep desired velocity at zero when appropriate so movement and gravity settle correctly.
- ParrelSync clones should be clients; do not test with multiple hosts in clones.
- Steam multiplayer tests need separate Steam accounts.

## Architecture Boundaries

- Data layer: ScriptableObjects, plain data structures, configs. Store values, not behavior-heavy logic.
- Interface / contract layer: `IState`, attackable contracts, and similar capability definitions.
- Core logic layer: state machine, movement control, KCC wrapping. Avoid direct UI/network coupling where possible.
- Network layer: Mirror SyncVar, Command, ClientRpc, TargetRpc. Keep it focused on synchronization and authority boundaries.
- Presentation layer: Animator, VFX, SFX, Cinemachine, and UI display.

When giving or changing code, briefly identify which layer the change belongs to and why.

## Naming, Constants, and Readability

- Avoid magic strings and magic numbers. Prefer `const`, `static readonly`, or `[SerializeField]` settings.
- Animator parameter names should be centralized with `Animator.StringToHash` when touching animation code.
- Use `#region` sections for larger scripts, especially SyncVar fields, Mirror callbacks, input, animation, networking, and helpers.
- Public methods should include a short XML summary when practical.
- Variable names should describe meaning, not implementation trivia.

## Performance Warnings

Proactively call out these risks when introducing or reviewing code:

- `FindObjectOfType` / `GetComponent` inside `Update()`.
- Per-frame `[Command]` calls without throttling.
- `foreach` in hot paths that may allocate or add avoidable GC pressure.
- URP-incompatible legacy shader patterns such as `_GrabPass`.

## Teaching and Response Style

- Simple questions get short direct answers.
- Explain one new concept at a time.
- Prefer the smallest runnable version first; leave advanced variants for follow-up.
- When using a new term, explain it in parentheses.
- After giving code, include:
  - What this code does in one sentence.
  - New concept used, if any.
  - Performance or architecture note, if relevant.
- If the user is stuck on the same issue, stop adding more code. Break the problem into 2-3 smaller steps and ask where they are stuck.
- Do not give a long complete copy-paste solution before the user understands the idea; for code over about 20 lines, split it into smaller parts and explain behavior after each part.

## High-Risk Areas

For these areas, answer with: "這部分屬於底層架構，建議先問主程 Ki fun，確認後再動，避免架構衝突。"

- NetworkManager-related changes.
- Transport switching logic.
- KinematicCharacterController low-level setup.
- State machine `IState` interfaces or ScriptableObject data architecture.
- Changes that affect networked prefab components or objects visible to all players.

## Unity Lifecycle Reminders

- `Awake()`: object setup before `Start()`; `isLocalPlayer` is not reliable yet.
- `Start()`: general setup; common place to disable non-local player KCC motor.
- `OnStartClient()`: client network initialization; apply initial SyncVar-driven visual state when needed.
- `Update()`: per-frame logic; avoid expensive lookups and per-frame network sends.
- `FixedUpdate()`: physics timing; do not use it blindly for KCC unless the system expects it.
- `OnDestroy()`: cleanup subscriptions and null-sensitive references.
