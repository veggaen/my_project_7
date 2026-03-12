# Pistol Shoulder Swap Spec

This is the intended third-person pistol behavior for the player.

## Control Rule

- Camera shoulder side controls the lead shooting side.
- Right shoulder camera means right-hand lead.
- Left shoulder camera means left-hand lead.

## Hipfire Behavior

### Right Shoulder

- Right hand is the firing hand.
- Right arm is raised in a one-handed pistol aim pose.
- Left arm stays relaxed / lowered.
- Torso is biased so the right shoulder is the lead shoulder.

### Left Shoulder

- Left hand is the firing hand.
- Left arm is raised in a one-handed pistol aim pose.
- Right arm stays relaxed / lowered.
- Torso is biased so the left shoulder is the lead shoulder.

## ADS / Steady Aim Behavior

### Right Shoulder ADS

- Right hand remains the firing hand.
- Left hand rises and supports the pistol.
- Both hands form the steady two-handed aim.
- Camera remains on the right shoulder.

### Left Shoulder ADS

- Left hand remains the firing hand.
- Right hand rises and supports the pistol.
- Both hands form the steady two-handed aim.
- Camera remains on the left shoulder.

## Shoulder Swap Behavior

- Shoulder swap is triggered by the existing shoulder toggle input.
- During the swap, the pistol transitions across the chest.
- The body rotates so the new lead shoulder comes forward.
- At the end of the swap:
  - right shoulder => right-hand lead pose
  - left shoulder => left-hand lead pose

## Animgraph Intent

Recommended state/model setup:

1. `pistol_right_hipfire`
2. `pistol_left_hipfire`
3. `pistol_right_ads`
4. `pistol_left_ads`
5. Optional additive `pistol_shoot`
6. Optional additive `pistol_reload`

Recommended parameter use:

1. `lead_side`
   - `1` = right lead
   - `-1` = left lead
2. `aim`
   - `false` = hipfire pose
   - `true` = ADS pose
3. `shoulder_swap_progress`
   - blend right-lead and left-lead during camera swap
4. `shoot`
   - self-reset additive fire layer
5. `reload`
   - self-reset additive reload layer

## Important Constraint

The stock `CitizenAnimationHelper` setup does not provide this mirrored pistol behavior by itself for this player.
The behavior above requires a custom body animgraph or authored left/right pose states.