---
name: numstrata-storage-io
description: >-
  Enforces NumStrata local and cloud storage I/O rules to minimize latency,
  protect FPS, and reduce server overage. Use when implementing or reviewing
  save/load, LocalDataManager, PlayerData, SessionResume, PlayerPrefs,
  serialization, Firebase/Firestore sync, cloud cache, dirty flags, conflict
  resolution, or any file/network persistence in this project.
---

# NumStrata — Storage I/O Optimization

**Goal:** Minimize latency, protect framerate, and control cloud I/O cost. Any I/O that violates these rules is a bad smell — refactor before shipping.

Source notes: [Skill.md](../../../Skill.md) at repo root.

**Project save spec (locked):** [reference.md](./reference.md) — schema, campaign state machine, milestones, corrupt handling. Read before implementing `LocalDataManager`, `PlayerSaveModels`, or campaign hooks.

## Pre-flight (required before writing I/O code)

Answer all three; if any answer is wrong, redesign:

1. **Main thread:** Does this block the game thread during gameplay? (sync file write in `Update`, mid-action, or UI frame)
2. **Payload size:** Are serialized strings bloated with redundant keys/nested objects?
3. **API churn:** Are cloud reads/writes inside a loop or on every UI open?

---

## Part 1 — Local storage

### Never block the main thread

- Sync file I/O in `Update()` or mid-action causes stutter/spikes.
- **Do:** `async`/`await`, or push large JSON/binary writes to a background thread; only touch Unity APIs on the main thread after completion.

### Batch writes (milestones only)

- **Do not** call disk save on every small change (e.g. each gold pickup).
- **Do:** Keep changes in RAM during play; flush to disk only at **milestones**:
  - Level cleared / failed
  - Chest opened / major reward
  - `OnApplicationPause` / `OnApplicationQuit`
- In this project, prefer extending `LocalDataManager` with explicit `MarkDirty()` + deferred flush rather than calling `SaveData()` from hot paths.

### Compact serialization

- Avoid naive JSON for large or frequent saves (string keys add bulk).
- **Do:** Flatten before serialize — prefer primitive arrays over deep object graphs.
- Example pattern for game logic payloads: `[a, b, c, d, op]` instead of nested metadata-heavy structures.

### Basic integrity (anti-cheat)

- **Do not** store plaintext like `{"gold": 9999}` without protection.
- **Do:** At minimum encode (Base64/XOR + salt) and append integrity hash (MD5/SHA). On mismatch → reject, reset, or restore backup.

---

## Part 2 — Cloud storage

Cloud I/O is a **cost** problem, not only performance.

### Smart cache

- **Do not** fetch from cloud on every menu open.
- **Do:** Pull once at startup (or on explicit refresh), cache locally; UI reads **only** from local cache.

### Lazy sync (`isDirty`)

- **Do not** write to cloud on every player action.
- **Do:** Apply state locally immediately (responsive UI), set `isDirty = true`, then batch:
  - Timer (e.g. 30–60s), **or**
  - Important events (level end, purchase confirm)
  - One payload per flush — **one write**, not dozens.

### Conflict resolution

- Every save bundle must include **`timestamp`**.
- On sync:
  - `Local_Timestamp > Cloud_Timestamp` → update cloud
  - `Cloud_Timestamp > Local_Timestamp` → prompt user or auto-pick per game design (document choice in code comments)

### Idempotent network writes

- Mutating requests need a unique **`TransactionID` (UUID)**.
- Server/function must ignore duplicate IDs and return success (prevents double spend on retry).

---

## Unity / NumStrata mapping

| Concern | Project touchpoints |
|--------|---------------------|
| Player profile | `LocalDataManager` → `PlayerData.json` under `Application.persistentDataPath` |
| Mid-session autosave | `SessionResume.json` — still milestone/throttled, not per-frame |
| Level definitions | `Resources/Campaign/{levelId}` — read-only, not player save |
| Menu → gameplay handoff | `CampaignSession.PendingLevelId` in `PlayerPrefs` (small, ephemeral) |

When changing `LocalDataManager.SaveData()` call sites, grep for `SaveData(` and verify each call is milestone-appropriate.

---

## Review checklist

- [ ] No sync disk I/O on main thread during active gameplay
- [ ] Saves batched to milestones + pause/quit
- [ ] Serialized payload flattened/minimized
- [ ] Local save has basic integrity check if player-editable
- [ ] Cloud: local cache for UI; no read spam
- [ ] Cloud: `isDirty` batching; single payload per flush
- [ ] Cloud: timestamps on merge; idempotent transaction IDs for spends
