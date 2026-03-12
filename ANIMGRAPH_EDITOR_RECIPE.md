# Animgraph Editor Recipe

This is the shortest clean path from the current code scaffold to a real third-person pistol body graph.

Use this for the body `SkinnedModelRenderer` on `player_vegga.prefab`.

## Goal

Build one custom body animgraph that supports all four required pistol poses:

1. Right shoulder hipfire
2. Right shoulder ADS
3. Left shoulder hipfire
4. Left shoulder ADS

And one controlled shoulder handoff between them.

## Before You Start

Code is already feeding these parameters into the body renderer:

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

You do not need more movement code before making the first working graph.

## Assets To Author First

Author these animation assets or pose sources first. Keep them minimal.

1. `pistol_right_hipfire`
2. `pistol_right_ads`
3. `pistol_left_hipfire`
4. `pistol_left_ads`
5. `pistol_shoot_add`
6. `pistol_reload_add`
7. `pistol_aim_matrix`

If you want a fast first win, the first four are mandatory and the rest can be temporary placeholders.

## Create The Graph Asset

1. Create a new animgraph asset for the body.
2. Assign it to the body `SkinnedModelRenderer` in `Assets/player_vegga.prefab`.
3. Keep `CitizenAnimationHelper` on the prefab for fallback values, but let the custom graph drive the body once assigned.

After assignment, `PlayerVeggaMovement` will stop applying the old pistol IK pose logic automatically.

## Parameter Setup In The Graph

Create these parameters in the graph with matching names and types:

1. Bool: `aim`
2. Bool: `shoot`
3. Bool: `reload`
4. Bool: `shoulder_swapping`
5. Bool: `third_person`
6. Bool: `grounded`
7. Bool: `crouching`
8. Bool: `sprinting`
9. Bool: `has_weapon`
10. Float: `aim_weight`
11. Float: `support_hand_weight`
12. Float: `move_speed`
13. Float: `move_forward`
14. Float: `move_right`
15. Float: `shoulder_swap_progress`
16. Float: `aim_yaw`
17. Float: `aim_pitch`
18. Int or Enum-compatible selector: `holdtype`
19. Int or Enum-compatible selector: `lead_side`

For `shoot`, enable auto reset if your graph setup uses a one-frame trigger path.
For `reload`, only use auto reset if your reload state machine is also built around a trigger pulse.

## Top-Level Layout

Build the graph in this order from left to right:

1. Locomotion group
2. Pistol upper-body group
3. Aim offset layer
4. Shooting additive machine
5. Reload additive machine
6. Final output

Do not start with IK. Build correct authored poses first.

## Step 1: Locomotion Group

Create a group named `locomotion_base`.

Inside it:

1. Add a 2D blend for grounded movement.
2. Drive the blend with `move_forward` and `move_right`.
3. Put idle, forward, backward, strafe left, and strafe right into the blend.
4. Add a crouch branch if you already have crouch locomotion assets.
5. Add a sprint branch if you already have sprint assets.
6. Gate airborne fallback with `grounded`.

The result should be a clean lower-body/base-body motion source without pistol-specific posing yet.

## Step 2: Pistol Upper-Body Group

Create a group named `pistol_pose` after `locomotion_base`.

Inside it, make this chain:

1. A `lead_selector` branch or blend that chooses right-lead versus left-lead.
2. A `right_pose` blend from `pistol_right_hipfire` to `pistol_right_ads` driven by `aim_weight`.
3. A `left_pose` blend from `pistol_left_hipfire` to `pistol_left_ads` driven by `aim_weight`.
4. A handoff blend between `right_pose` and `left_pose` driven by `shoulder_swap_progress`.
5. A final selector that uses the handoff blend while `shoulder_swapping` is true, otherwise snaps to the active `lead_side` pose.

Practical rule:

1. `lead_side = 1` means right lead.
2. `lead_side = -1` means left lead.
3. If enum selectors are easier in the editor, map them there and keep the code values documented.

Use a bone mask so this group mainly affects torso, clavicles, arms, and hands. Let locomotion own most of the legs.

## Step 3: Support-Hand Fade

The main pose problem you had was not just weapon side, but off-hand behavior.

Add a controlled support-arm layer:

1. Create a variant or additive layer that raises the support hand into the pistol only for ADS.
2. Drive its blend weight with `support_hand_weight`.
3. Keep the support arm near zero in hipfire.
4. Bring it to full only in ADS and during handoff if needed.

This is the parameter that should make:

1. Right shoulder ADS raise the left hand.
2. Left shoulder ADS raise the right hand.

Do not use this layer to invent lead-hand posture. That comes from the base authored lead-side poses.

## Step 4: Aim Offset Layer

Create a group named `aim_offset` after `pistol_pose`.

Recommended setup:

1. Use an aim matrix or equivalent directional pose blend.
2. Drive it with `aim_yaw` and `aim_pitch`.
3. Use the neutral pistol aim pose as the reference frame for the additive or delta.
4. Apply it mainly to spine, chest, neck, and optionally clavicles.
5. Keep the hands from drifting too far by limiting the pose range before adding IK.

This follows the guide pattern in `fishdevguide`: aim matrix is more stable than trying to solve large vertical angles with look-at alone.

## Step 5: Shooting Additive Machine

Create a state machine named `shoot_machine`.

States:

1. `idle`
2. `shoot`

Transitions:

1. `idle -> shoot` on `shoot == true`
2. `shoot -> idle` on finished condition

Settings:

1. Set `idle` as the start state.
2. Set `idle -> shoot` blend time to zero or nearly zero.
3. Use a non-looping additive shot animation for `shoot`.
4. If you need rapid fire, use the guide approach: let `idle` always evaluate so it can retrigger immediately.

Apply this state machine additively on top of `aim_offset`.

## Step 6: Reload Additive Machine

Create a state machine named `reload_machine`.

States:

1. `idle`
2. `reload`

Transitions:

1. `idle -> reload` on `reload == true`
2. `reload -> idle` on finished condition

Settings:

1. Set `idle` as the start state.
2. Use a non-looping reload additive.
3. Keep reload above shoot in authoring priority if both can overlap visually, or explicitly block one path.

Apply reload after shooting if reload should dominate arm motion.

## Step 7: Optional IK Pass

Only add IK after the four poses already look correct without it.

Recommended uses:

1. Lock the support hand to a right-hand child target or weapon helper bone.
2. Correct small wrist drift in ADS.
3. Stabilize the off-hand during the shoulder handoff.

Do not use IK to fully create the left-shoulder stance. That was the failure mode you already hit in code.

If you use the guide pattern, create a helper target bone under the lead hand and let the support hand follow it with a short chain.

## Minimal Node Order

If you want one concrete left-to-right graph to build first, use this order:

1. `locomotion_base`
2. `upper_body_mask`
3. `right_ads_blend`
4. `left_ads_blend`
5. `shoulder_handoff_blend`
6. `lead_side_selector`
7. `support_hand_add`
8. `aim_offset`
9. `shoot_machine`
10. `reload_machine`
11. `final`

That gives you the exact visible feature you are missing before you spend time polishing anything else.

## Preview Checklist In Editor

Test these parameter sets directly in the graph preview.

### Right shoulder hipfire

1. `lead_side = 1`
2. `aim = false`
3. `aim_weight = 0`
4. `support_hand_weight = 0`

Expected result: right shoulder forward, right hand leads pistol, left hand low.

### Right shoulder ADS

1. `lead_side = 1`
2. `aim = true`
3. `aim_weight = 1`
4. `support_hand_weight = 1`

Expected result: right hand leads, left hand supports, torso tightens.

### Left shoulder hipfire

1. `lead_side = -1`
2. `aim = false`
3. `aim_weight = 0`
4. `support_hand_weight = 0`

Expected result: left shoulder forward, left hand leads pistol, right hand low.

### Left shoulder ADS

1. `lead_side = -1`
2. `aim = true`
3. `aim_weight = 1`
4. `support_hand_weight = 1`

Expected result: left hand leads, right hand supports, mirrored torso posture.

### Shoulder handoff

Sweep these values while previewing:

1. Start with `lead_side = 1`
2. Set `shoulder_swapping = true`
3. Move `shoulder_swap_progress` from `0` to `1`
4. End with `lead_side = -1`

Expected result: the pistol and upper body travel across the chest and land in a valid left-lead pose, not a neutral broken pose.

## Acceptance Standard

The graph is good enough for the next phase when all of these are true:

1. Left shoulder visibly changes the full upper-body stance, not just weapon attachment.
2. Hipfire and ADS are different on both shoulders.
3. The off-hand stays lowered in hipfire and supports only when intended.
4. Shoulder swap blends between two authored stances instead of twisting a single stance.
5. Shoot and reload can play without destroying the lead-side pose.

## What Not To Do

1. Do not try to mirror the stock citizen pistol pose with only runtime offsets again.
2. Do not start with hand IK before the left and right lead poses exist.
3. Do not let leg stance live entirely inside the pistol upper-body layer.
4. Do not use looping shot or reload additives for trigger-style actions.

## First Real Milestone

Your first milestone is not a perfect combat animation system.

It is this:

1. Assign a custom body animgraph.
2. Make the four screenshot states preview correctly.
3. Verify in-game that left shoulder finally changes the body pose.

Once that works, then it is worth polishing aim offsets, hand IK, and locomotion quality.