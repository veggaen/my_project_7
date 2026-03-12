# Pistol Shoulder Swap Spec

This document defines the correct long-term architecture for third-person pistol stances.
It exists to stop further camera-space IK experimentation and keep the project moving toward
an authored, animgraph-driven solution.

## Root Diagnosis

The current bugs came from the wrong layer owning the problem.

- Code tried to invent a missing left-lead stance using offsets and IK.
- The stock Citizen pistol graph still assumes a right-hand primary pose.
- Moving the gun or hands in runtime without matching authored body poses creates rig conflict.
- That conflict showed up as floating guns, twisted wrists, wrong-hand aiming, stiff rest poses, and bad shoulder transitions.

The conclusion is simple:

- Code chooses state.
- Animgraph owns the pose.
- IK only preserves secondary contact.

## Target Player Behavior

### Right Shoulder Hipfire

- Right hand is the primary firing hand.
- Left arm is relaxed and lowered.
- Torso and shoulder bias are right-lead.

### Right Shoulder ADS

- Right hand remains primary.
- Left hand rises into a support grip.
- Torso becomes more neutral and stable.

### Left Shoulder Hipfire

- Left hand is the primary firing hand.
- Right arm is relaxed and lowered.
- Torso and shoulder bias are left-lead.

### Left Shoulder ADS

- Left hand remains primary.
- Right hand rises into a support grip.
- Torso becomes more neutral and stable.

### Shoulder Swap Transition

- The pistol crosses the chest during the handoff.
- The torso rotates toward the new lead side.
- Shoulders and feet pivot with the transfer.
- The swap is a short authored transition, not an IK teleport.

## Responsibility Split

### Code

Code is responsible for gameplay truth and parameter driving only.

- Determine current shoulder side.
- Determine hipfire vs ADS.
- Determine movement, crouch, sprint, grounded, reload, and shoot state.
- Drive animgraph parameters.
- Keep firing aligned to the crosshair.
- Keep worldmodel attachment stable and body-relative.

Code is not responsible for:

- inventing lead-hand body poses
- solving wrist orientation by offsets
- moving the weapon from camera space
- replacing the handoff transition with transform hacks

### Animgraph / Authored Animation

The animgraph owns the actual body solution.

- `pistol_right_hipfire`
- `pistol_right_ads`
- `pistol_left_hipfire`
- `pistol_left_ads`
- `pistol_shoulder_handoff`
- additive `pistol_shoot`
- additive `pistol_reload`

The graph should blend from parameters instead of being driven by manual runtime transforms.

### IK

IK is allowed only for secondary contact and cleanup.

- support hand locking in ADS
- support hand continuity during shoulder handoff
- optional small stabilization after authored pose selection

IK is not the primary stance system.

## Runtime Parameter Contract

These parameters already exist or are now written by runtime code:

1. `holdtype`
   - weapon family selector
2. `holdtype_handedness`
   - stock Citizen handedness selector
   - `0` = both
   - `1` = right
   - `2` = left
3. `aim`
   - bool ADS state
4. `aim_weight`
   - `0..1` hipfire to ADS blend
5. `lead_side`
   - custom animgraph selector
   - `1` = right lead
   - `-1` = left lead
6. `shoulder_swap_progress`
   - `0..1` transition value through handoff
7. `shoulder_swapping`
   - bool for explicit handoff state gating
8. `support_hand_weight`
   - `0..1` support-hand contribution
9. `shoot`
   - self-reset fire trigger
10. `reload`
   - self-reset reload trigger
11. movement params
   - `move_speed`, `move_forward`, `move_right`, `grounded`, `crouching`, `sprinting`
12. aim offsets
   - `aim_yaw`, `aim_pitch`

## Implementation Phases

### Phase 1: Runtime Cleanup

- Keep runtime parameter-only.
- Do not reintroduce camera-space hand IK.
- Keep the temporary stock fallback stable.

### Phase 2: Content Authoring

- Author left-lead pistol poses.
- Author the shoulder-handoff transition clip.
- Author support-hand contact positions.

### Phase 3: Custom Animgraph Integration

- Build a state machine that reads `lead_side`, `aim`, and `shoulder_swap_progress`.
- Blend hipfire/ADS inside each lead-side branch.
- Use `shoot` and `reload` as additive overlays.

### Phase 4: Support-Hand IK

- Re-enable only support-hand IK.
- Bind it to the citizen IK helper workflow after pose selection.
- Never use it to define the primary lead stance.

## Temporary Fallback

Until custom animation content exists:

- keep the stock Citizen pistol animation path stable
- keep the weapon body-anchored
- keep shooting aligned to the crosshair
- avoid all runtime pose invention

That temporary fallback is intentionally less ambitious than the target design, because stability is more important than another brittle partial solution.