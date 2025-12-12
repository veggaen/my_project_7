# Player Prefab – Required Components Cheat Sheet

This documents the components that should be on the **`player_vegga` prefab** (root GameObject and key children) based on the current working setup you have in the editor screenshots.

Use it as a checklist when something breaks or when recreating the prefab.

---

## Root GameObject: `player_vegga`

**Required components (in this order is typical but not required):**

1. **`Transform`**
   - Default on every GameObject.

2. **`CharacterController`**
   - Handles capsule collision and basic physics.
   - Key properties:
     - `Radius`
     - `Height`
     - `Step Height`
     - `Ground Angle`
     - `Max Force`, `Acceleration`, `Bounciness`

3. **`PlayerVeggaMovement`**
   - Core movement / input / animation sync.
   - Inspector links:
     - `Head` → child GameObject `Head`
     - `Body` → child GameObject `Body`

4. **`PlayerVeggaStats`**
   - HP, Armor, Money, Job and other core stats.
   - Important config:
     - `MaxHealth`, `InitHealth`
     - `MaxArmor`, `InitArmor`
     - `StartMoney` (default 500)
     - `StartJobName` (default `"Citizen"`)
     - `MaxPrayer`, `MaxStamina`, `MaxSpecialAttack`
   - Group **Runtime Stats** is read‑only while playing.
   - Group **Testing** has buttons:
     - `Deal Damage`, `Heal To Full`
     - `Add $100`, `Add $1000`
     - `Set Full Armor`, `Remove All Armor`
     - `Kill Player`

5. **`PlayerVeggaSkills`**
   - OSRS‑style skills + combat level.
   - Inspector:
     - `Stats` reference → same `PlayerVeggaStats` on this GameObject.
   - Read‑only sections:
     - `Combat Info` (Combat Level, Type, Next Combat Hint)
     - `Combat Skills`, `Gathering Skills`, `Production Skills`, `Utility Skills`.
   - Group **Testing**:
     - `Add 100 Attack XP`, `Add 100 Strength XP`, etc.
     - `Set All Combat to 99`, `Reset All Skills to 1`.

6. **`PlayerSessionStats`**
   - Tracks meta stats: kills, deaths, connects, playtime, etc.
   - Used for scoreboard + name resolution.

7. **`HUDManager`**
   - Links this player’s `PlayerVeggaStats` to the UI HUD.
   - No manual links required; finds `PlayerHud` in scene and binds automatically.

8. **`PlayerVeggaDeath`**
   - Death detection, ragdoll, respawn.
   - Key properties:
     - `Respawn Delay` (seconds)
     - `Death Camera Distance`
     - `Drop Items On Death` (bool)

9. **`VeggaInventory`**
   - Core inventory data for this player.
   - Works with `InventoryHud` on UI Root.

10. **`PlayerSalary`**
    - (New) Periodic job salary.
    - Inspector:
      - `Stats` → same `PlayerVeggaStats` (or leave blank to auto‑find).
      - `SalaryIntervalSeconds` → default `300` (5 minutes).

---

## Child: `Head`

**Components:**

1. `Transform`
2. **(Usually)** `CameraComponent`
   - Used by `CameraVeggaMovement` to position the view.
3. Any optional helpers (e.g. audio listener if needed).

This object’s transform is also driven by `CameraVeggaMovement` via `TargetHeadAngle`.

---

## Child: `Body`

**Components:**

1. `Transform`
2. `SkinnedModelRenderer`
   - Your citizen model.
3. `CitizenAnimationHelper`
   - Drives animations from movement.

`PlayerVeggaMovement` uses this object for body rotation, animation, and visibility.

---

## Scene Objects Related to Player

On your **`UI Root`** GameObject in the scene (not on the prefab), you should have:

- `ScreenPanel`
- `VeggaUiSettings`
- `HUDManagerScene`
- `PlayerHud`
- `XPBar`
- `VeggaScoreboard`
- `InventoryHud`
- `CrossVeggaHair`
- `CrossVeggaHairMenu`
- `VeggaPauseMenu`
- `VeggaHudLayoutOverlay`

On your **system/manager** GameObject (e.g. `SystemDataManager` or similar):

- `Network Helper`
- `PlayerDataPersistence`
- Any other global systems (e.g. spawn manager).

---

## Quick Checklist (Player Prefab)

When something breaks, verify on `player_vegga` root:

- [ ] `CharacterController`
- [ ] `PlayerVeggaMovement` (Head/Body wired)
- [ ] `PlayerVeggaStats`
- [ ] `PlayerVeggaSkills` (Stats wired)
- [ ] `PlayerSessionStats`
- [ ] `HUDManager`
- [ ] `PlayerVeggaDeath`
- [ ] `VeggaInventory`
- [ ] `PlayerSalary` (optional but recommended)
- [ ] Child `Head` exists and is referenced
- [ ] Child `Body` exists and has `SkinnedModelRenderer` + `CitizenAnimationHelper`


