# Custom Animgraph Scaffold

This project now includes a dedicated animgraph driver component on the player prefab.

What is wired already:

- `PlayerVeggaAnimGraphDriver` writes anim parameters directly to the body renderer.
- `VeggaEquipmentController` exposes `ShotSequence` and `ReloadSequence` so self-resetting animgraph trigger params can be driven without touching input code again.
- `player_vegga.prefab` now has the animgraph driver attached and targeting the existing body renderer.
- `PlayerVeggaMovement` now automatically stops applying the old CitizenAnimationHelper pistol/IK pose logic once a real custom body animgraph is assigned.

Parameters provided by code:

- `holdtype`
- `aim`
- `shoot`
- `reload`
- `lead_side`
- `shoulder_swap_progress`
- `shoulder_swapping`
- `third_person`
- `aim_weight`
- `move_speed`
- `move_forward`
- `move_right`
- `grounded`
- `crouching`
- `sprinting`
- `aim_yaw`
- `aim_pitch`

Recommended next editor steps:

1. Duplicate the player body setup into a custom animgraph asset instead of relying only on `CitizenAnimationHelper` behavior.
2. Add left-lead and right-lead pistol states driven by `lead_side`.
3. Blend between those states using `shoulder_swap_progress`.
4. Keep `shoot` and `reload` as additive/self-resetting layers.
5. Use IK only for final hand locking and ADS support, not for inventing the whole left-side pose.

Suggested high-quality graph structure:

1. Base locomotion blendspace driven by `move_forward`, `move_right`, `move_speed`, `grounded`, `crouching`, and `sprinting`.
2. Upper-body aim offset driven by `aim_yaw` and `aim_pitch`.
3. Pistol lead-side selector driven by `lead_side`.
4. Shoulder handoff transition driven by `shoulder_swap_progress` and `shoulder_swapping`.
5. ADS selector driven by `aim`/`aim_weight`.
6. Additive `shoot` and `reload` layers on top.

Concrete editor build order now lives in `ANIMGRAPH_EDITOR_RECIPE.md`.

Important editor workflow note:

- Assign the first custom body animgraph on `Assets/player_vegga.prefab` itself, not only on a scene instance.
- If you experiment on a scene instance, remember that the graph assignment is just an override until you apply it back to the prefab.
- If the graph seems to "disappear" after reopening a scene, verify whether the source prefab was ever updated.

Current intent:

- Right shoulder remains on the stable stock-citizen behavior.
- Left shoulder will only become a true mirrored tactical pose once a custom animgraph is assigned to the body renderer.
- The new code path is in place so that assigning that animgraph becomes an editor/content task rather than another movement-code rewrite.