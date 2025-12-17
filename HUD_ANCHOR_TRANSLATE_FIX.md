# HUD Anchor / Translate Bug Fix Notes

## The root bug
Your HUD layout presets were **mathematically correct**, and the layout overlay (blue/pink boxes) proved it.

The mismatch came from how runtime UI panels were anchored:

- The system was positioning panels using `left/top` plus an **anchor adjustment** done via CSS `transform: translate(...)`.
  - Example: `BottomLeft` typically needs `translateY(-100%)`.
- On your setup, that translate-based anchoring was **not being applied consistently** to the runtime panel placement.
- Result: Bottom / right presets ended up off-screen by exactly **full height** or **half height**, which shows up as deltas like:
  - $\Delta y \approx h$ (full height)
  - $\Delta y \approx h/2$ (half height)

This is the classic signature of “translate anchoring didn’t take effect”.

## How we proved it
We compared:

- **Expected top-left** (from layout math)
- **Runtime rect** (what the engine actually drew)

The delta matched the anchor offsets (full/half height), confirming the runtime placement wasn’t honoring translate anchoring.

## The fix (important part)
In [Code/UI/Hud/VeggaHudLayoutApply.cs](Code/UI/Hud/VeggaHudLayoutApply.cs) we stopped relying on `transform: translate(...)` for anchoring.

Instead, we compute the **true top-left** in pixels directly:

$$
\text{topLeftPx} = \text{anchorPointPx} + \text{AnchorOffsetPx}(anchor, baseSizePx)
$$

Then we write that top-left into `left/top` (percentages relative to the panel’s parent).

We keep `transform` only for scaling:

- `transform-origin: 0% 0%`
- `transform: scale(x)`

## Why that fixed all presets
Right/bottom presets no longer depend on “CSS translate anchor math.”

Even if translate behaves weirdly (or is ignored), the element still lands correctly because `left/top` already includes the anchor offset.

## One-liner explanation
“We fixed it by baking anchor offsets into `left/top` instead of using `transform: translate(...)` for anchoring; translate was why bottom/right presets were wrong.”
