# Dual Wield Pistol Spec

This is the runtime gameplay contract for the first dual-pistol implementation pass.

## Goal

Add a playable dual-pistol mode without pretending the final third-person animation content already exists.

## Runtime Rules

### Single Pistol

- `Mouse1` fires the active pistol.
- `Mouse2` enters ADS.
- ADS slightly narrows FOV.
- ADS slows movement speed.
- Shoulder side determines the intended lead side for future animation and fire-side params.

### Dual Pistols

- `Mouse1` fires the camera-side pistol.
- `Mouse2` fires the opposite-side pistol.
- Dual pistols do not enter ADS.
- Each pistol has its own magazine count.
- Reserve ammo is shared.
- Reload refills the left magazine first, then the right magazine.

## Test Flow

Use the host-side debug command:

1. `vegga_give_pistol_kit`

This gives:

1. slot 1: `P250`
2. slot 2: `Dual P250s`
3. slot 3: `MP5`
4. shared `9mm` reserve ammo

The command also auto-equips slot 1 so single-pistol testing starts immediately.

## Current Constraint

The runtime can now support dual-wield input, separate ammo state, HUD display, and animation parameters.
It still does not solve the missing authored left/right dual-wield body poses.

That means:

- gameplay state is now scaffolded correctly
- UI can represent separate magazines
- animgraph can read `dual_wield` and `fire_side`
- full visual quality still depends on authored animation content

## Future Animation Requirements

For a proper dual-wield presentation, add at least:

1. `dual_right_lead_idle`
2. `dual_left_lead_idle`
3. `dual_right_lead_fire`
4. `dual_left_lead_fire`
5. `dual_shoulder_handoff`

And use runtime params:

1. `lead_side`
2. `fire_side`
3. `dual_wield`
4. `shoulder_swap_progress`