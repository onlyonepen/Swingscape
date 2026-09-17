# Standalone Player System (from Swingscape)

A self-contained, grapple/swing-focused first-person player controller extracted from
**Swingscape**. It has been **decoupled from the game's enemy system** so it can drop into
any Unity project without dragging the rest of Swingscape along.

Core mechanics included: walking / jumping / crouch-slide (floating-capsule physics),
wall-running, mantling, sliding, a grapple-hook state machine (throw → swing → pull /
pull-in / leap / rope-retract), swing-dash, a melee combo system with hit-stop, an energy
pool, HP with i-frames + death, camera FOV control, speed lines, footsteps and audio.

---

## 1. Installation

1. Copy the entire `StandalonePlayerSystem/` folder **into your project's `Assets/`** folder
   (e.g. `Assets/StandalonePlayerSystem/`). Unity will generate fresh `.meta` files on import.
2. Install the required packages (section 2).
3. Do the one-time scene setup (section 4).
4. Build the player GameObject (section 5) — no prefab is shipped; wiring is per-project.

> **Note:** This folder deliberately lives **outside** the original project's `Assets/`
> directory so it doesn't compile twice inside Swingscape. In *your* project it must go
> **inside** `Assets/`.

---

## 2. Required packages / plugins

These must exist in the target project or the scripts won't compile:

| Dependency | Used by | Notes |
|---|---|---|
| **DOTween** (`DG.Tweening`) | PlayerStateManager, PlayerHpManager, WallRunningState, MantleState, GrapplePullState | Free asset (Demigiant) or DOTween Pro. |
| **VInspector** | PlayerStateManager, PlayerHpManager | Inspector-attribute plugin (`[Button]`, `[ReadOnly]`, `[Tab]`, `[Foldout]`, etc.). Available on OpenUPM / Asset Store. If you'd rather not add it, strip those attributes from the two files. |
| **Universal Render Pipeline (URP)** | SpeedLineManager (`UnityEngine.Rendering.Universal`) | Needed for the speed-line full-screen effect. On a non-URP project, remove `SpeedLineManager`. |
| **Unity UI** (`com.unity.ugui`) | EnergyBar, PlayerStateManager, PlayerHpManager | Built-in. |
| **Physics** (3D) | Everything (Rigidbody, SpringJoint, Collider) | Built-in. |

**Optional:** The input layer is abstracted behind `IPlayerInput`. The bundled `PlayerInput`
uses the **legacy Input Manager** (`UnityEngine.Input`), so the new Input System package is
**not** required. To use the new Input System instead, write your own `IPlayerInput`
implementation — nothing else changes.

---

## 3. Folder contents

```
StandalonePlayerSystem/
├── Player/
│   ├── PlayerManager.cs            # central hub — resolves all sub-systems
│   ├── PlayerStateManager.cs       # locomotion/grapple FSM + PlayerState base class
│   ├── PlayerBaseMovement.cs       # walk/jump/crouch/slide, floating capsule, camera
│   ├── PlayerCameraController.cs   # FOV control
│   ├── PlayerAttacking.cs          # melee combo + hit-stop
│   ├── PlayerAttackArea.cs         # melee hitbox
│   ├── PlayerEnergy.cs             # energy pool (grapple ability costs)
│   ├── GrappleTargeting.cs         # aim-assist target selection + reticle
│   ├── SpeedLineManager.cs         # URP speed lines + wind SFX
│   ├── EnergyBar.cs                # energy UI
│   ├── PlayerHpManager.cs          # HP, i-frames, death event
│   ├── Input/  (IPlayerInput, PlayerInput)
│   ├── Weapons/ (IWeaponMode)
│   └── States/ (PlayerBaseState, ThrowGrappleState, SwingState, GrapplePullState,
│                GrapplePullintoState, GrappleLeapState, PullBackRopeState,
│                WallRunningState, MantleState, SlideState, DiedState)
└── Dependencies/
    ├── GlobalReference.cs          # scene singleton: player ref + layer masks
    ├── HitStopUtil.cs              # scene singleton: time-scale / hit-stop owner
    ├── AudioManager.cs             # scene singleton: pooled audio player
    ├── AudioDataSO.cs              # ScriptableObject audio definition
    ├── FootstepManager.cs          # speed-driven footstep loop
    ├── IDamagable.cs               # damage interface (SplitDeath)
    ├── IParriable.cs               # parry interface (namespace Script.Enemy)
    ├── IGrappleInteractable.cs     # ★ decoupling seam for grapple targets
    └── PlayerCombatEvents.cs       # ★ decoupling event bus for "target killed"
```

★ = new files added during extraction (do not exist in the original project).

---

## 4. One-time scene setup

Three singleton components must exist in every scene that uses the player:

1. **GlobalReference** — put on a GameObject in the scene and:
   - assign `player` → your player's `PlayerManager`;
   - set the `playerLayer`, `TerrainLayer` and `EnemyLayer` LayerMasks.
2. **HitStopUtil** — put on any GameObject. Owns `Time.timeScale`; melee slow-mo/hit-stop needs it.
3. **AudioManager** — put on any GameObject and populate its **Audio Library** with `AudioDataSO`
   assets. The player looks up sounds by name:
   - `"ThrowGrapple"` (ThrowGrappleState)
   - `"Melee"` and `"MeleeHit"` (PlayerAttacking)
   Create `AudioDataSO` assets via **Create ▸ Swingscape ▸ Audio Data** and set `audioName` to match.

### Layers
Create these layers and assign them in `GlobalReference` and on your objects:
- **Player layer** — the player.
- **Terrain layer** — swingable static geometry (the grapple's "obstacle"/swing mask).
- **Enemy layer** — grappleable/pullable targets.

`GrappleTargeting` also exposes `HeavyPull` and other layer masks in its inspector for
classifying light vs. heavy pull targets — set these to taste.

---

## 5. Building the player GameObject (no prefab shipped)

The original `Player.prefab`, arm models and audio clips were **not** copied (they're
Swingscape-specific art/assets). Rebuild a player object like this:

1. Empty GameObject → add `Rigidbody` (freeze rotation), a `CapsuleCollider`, and a child
   `Camera`.
2. Add the player components: `PlayerManager`, `PlayerStateManager`, `PlayerBaseMovement`,
   `PlayerCameraController`, `PlayerEnergy`, `GrappleTargeting`, `PlayerAttacking`,
   `PlayerHpManager`, `FootstepManager`, and (URP only) `SpeedLineManager`.
3. Add a child GameObject with a component implementing `IPlayerInput` (the bundled
   `PlayerInput` works out of the box).
4. Wire the serialized fields in the inspector: `LineRenderer` for the rope, grapple gun-tip
   transform, arm `Animator`, reticle UI, HP container UI, energy bar, red-screen overlay and
   game-over screen for `DiedState`, etc. `PlayerManager` auto-resolves most sibling
   sub-systems in `Awake`, so you mainly wire the scene/UI/visual references.

Execution order note: `PlayerManager` runs at `[DefaultExecutionOrder(-50)]` so its references
are ready before siblings read them.

---

## 6. Connecting to YOUR enemies / grapple targets (the decoupling)

The player no longer knows about any `BaseEnemy` class. To make an object grappleable and
combat-reactive, implement these on it and put it on the **Enemy layer**:

- **`IGrappleInteractable`** — `GetPull()` (called when the player starts pulling it) and
  `Stagger(float time)` (called on swing-leap). The player finds it via
  `TryGetComponent<IGrappleInteractable>()`, so if your target doesn't implement it, the
  grapple still works — it just won't get the pull/stagger reaction.
- **`IDamagable`** — `SplitDeath(Transform plane)` for melee/pull kills.
- **`IParriable`** (optional) — `Parried()` for parry reactions.

**Refilling energy on kill:** the player's energy refills when you call
`PlayerCombatEvents.RaiseTargetKilled()`. Call it from your own enemy death code:

```csharp
public void Death()
{
    // ...your death logic...
    PlayerCombatEvents.RaiseTargetKilled();   // player energy refills
}
```

---

## 7. What was intentionally left out

- **`CheckpointManager` / `ExitAndEnding`** — Swingscape-specific level/floor progression and
  scene flow (used a `GameValue` static + `BaseEnemy[]` floor lists). Not part of a reusable
  player; re-implement respawn/level flow for your own game.
- **Prefab, arm models, animator controller, audio clips** — project-specific art/assets.
  Rebuild per section 5.

## 8. Known cosmetic notes

- A few state files keep a now-unused `using Script.Enemy;` line (the namespace still exists
  via `IParriable`, so it compiles). Safe to delete for cleanliness.
- `PlayerEnergy.UseEnergy` currently `return true`s early (energy costs bypassed) — this was
  preserved from the original. Remove that early return to enforce energy costs.
- `IParriable` is kept in its original `namespace Script.Enemy`; move it if you prefer.
