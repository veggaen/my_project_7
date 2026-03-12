# Animgraph Build Checklist

This checklist maps directly to the four screenshots/poses you keep testing.

## The Four Required States

1. Right shoulder, no aim
   - Right hand is the lead pistol hand.
   - Right shoulder is forward.
   - Left hand stays lowered.
   - Feet and hips favor a right-lead stance.

2. Right shoulder, aim
   - Right hand remains the lead pistol hand.
   - Left hand rises to support the pistol.
   - Torso tightens into a steady ADS posture.

3. Left shoulder, no aim
   - Left hand is the lead pistol hand.
   - Left shoulder is forward.
   - Right hand stays lowered.
   - Feet and hips mirror into a left-lead stance.

4. Left shoulder, aim
   - Left hand remains the lead pistol hand.
   - Right hand rises to support the pistol.
   - Torso tightens into a mirrored ADS posture.

## Shoulder Swap Transition

Between right-lead and left-lead:

1. Weapon crosses the chest.
2. Torso rotates so the new lead shoulder comes forward.
3. Feet/hips pivot to the new stance.
4. Support hand can come up during the handoff to preserve visual control of the pistol.
5. End state must land in either right-lead or left-lead, never in a neutral floating-hand pose.

## Parameters Already Available In Code

From `PlayerVeggaAnimGraphDriver`:

1. `holdtype`
2. `aim`
3. `shoot`
4. `reload`
5. `lead_side`
6. `shoulder_swap_progress`
7. `shoulder_swapping`
8. `third_person`
9. `aim_weight`
10. `has_weapon`
11. `support_hand_weight`
12. `move_speed`
13. `move_forward`
14. `move_right`
15. `grounded`
16. `crouching`
17. `sprinting`
18. `aim_yaw`
19. `aim_pitch`

## Recommended Graph Layout

1. Base locomotion layer
   - Driven by `move_forward`, `move_right`, `move_speed`, `grounded`, `crouching`, `sprinting`.

2. Pistol stance selector
   - Branch to right-lead or left-lead pistol base based on `lead_side`.

3. Shoulder handoff blend
   - Blend right-lead and left-lead using `shoulder_swap_progress` when `shoulder_swapping` is true.

4. ADS layer
   - For each lead side, blend hipfire -> ADS using `aim` or `aim_weight`.
   - Use `support_hand_weight` to fade support-arm contribution in and out cleanly.

5. Upper-body aim offset
   - Driven by `aim_yaw` and `aim_pitch`.

6. Additive action layers
   - `shoot`
   - `reload`

## Minimal First Win

If you want the fastest path to visible improvement, author only these first:

1. `pistol_right_hipfire`
2. `pistol_right_ads`
3. `pistol_left_hipfire`
4. `pistol_left_ads`

Then wire:

1. `lead_side` to pick right vs left lead.
2. `aim` to pick hipfire vs ADS.
3. `shoulder_swap_progress` to blend between right and left lead during the swap.

That alone should fix the exact failure you keep showing in screenshots: the weapon side changes, but the body pose does nothing.