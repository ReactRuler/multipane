# 05 · Layout engine

`LayoutEngine` is a pure library: no WPF, no HWNDs. Input: mode, ordered visible sessions, content rectangle, saved sizes. Output: one rectangle per session plus the divider rectangles. The shell applies the rectangles to pane views; `WindowHost` applies the pane's inner rectangle to the hosted window.

## Model

```
Layout {
  mode: Tabs | Columns | Rows | Grid | Focus
  order: [profileId]          // layout order of all profiles, hidden ones included
  hidden: [profileId]
  activeTab: profileId        // Tabs mode
  focusProfile: profileId     // Focus mode's large pane
  maximized: profileId | null
  sizes: { Columns: [fraction], Rows: [fraction], Grid: { cols: [fraction], rows: [fraction] }, Focus: { main: fraction, strip: [fraction] } }
  poppedOut: { profileId: { x, y, w, h, monitor } }
  window: { x, y, w, h, state: Normal | Maximized, monitor }
}
```

Visible list = `order` minus `hidden`, in order. All fractions sum to 1 per axis and are stored per arrangement, so switching Columns → Grid → Columns returns to the same column widths.

## Computing rectangles

Content rectangle `R` = shell client area minus the 40 px title bar, with 6 px padding left, right and bottom. Gap `g` = 6 px. Minimum pane 320 × 240 px (logical). `n` = number of visible sessions.

- **Tabs**: the active tab gets all of `R`; others get an empty rectangle and are hidden.
- **Columns**: widths `w_i = (R.w − g·(n−1)) · f_i`; heights `R.h`.
- **Rows**: symmetric.
- **Grid**: `cols = ceil(sqrt(n))`, `rows = ceil(n / cols)`; so 2 → 2×1, 3 → 2×2 with the last pane spanning the second row, 4 → 2×2, 5 → 3×2 with the last pane spanning two cells, 6 → 3×2, 7–9 → 3×3. Cells are filled in order, left to right, top to bottom; the panes in an incomplete last row expand equally to fill it (3 = 2 + 1 full width, 5 = 3 + 2 at 1.5 cells each). Column and row fractions are saved separately.
- **Focus**: one large pane `R.w · f_main` wide, full height; the strip on the right (min 320 px) stacks the rest with fractions `strip[]`. Clicking the "make this the focus" button in a strip pane swaps `focusProfile`.
- **Maximised**: the maximised pane takes `R`; the others keep their rectangles but are hidden. Restore returns to the arrangement unchanged.

Every rectangle is rounded to whole device pixels after DPI scaling, so adjacent panes never share a blurry edge; the rounding error goes to the last pane on each axis.

Below the minimum size the engine clamps and reports `tooSmall = true`; the shell then shows a one-line notice in the title bar ("Window too small for 6 panes. Try Tabs or Grid.") rather than overlapping panes.

## Reorder

Drag or Ctrl+Shift+arrows change `order`. Only `order` changes; fractions stay attached to the *slot*, not the profile, because that matches what the eye expects (the big column stays big). Moving a pane into or out of `hidden` keeps its position in `order`, so "Show" returns it where it was.

## Resize

A divider between slots `i` and `i+1` on an axis moves `delta` px: `f_i += delta / axisLength`, `f_{i+1} −= same`, both clamped so neither pane goes below its minimum. Double-click on a divider sets the two to the mean of the two. Ctrl+Shift+0 sets every fraction on both axes to `1/n`.

## Mode switches

Switching mode changes only `mode`; nothing else is touched, which is what makes the switch a 150 ms re-layout with no reload. Tabs → arrangement: `activeTab` becomes the focused pane. Arrangement → Tabs: the focused pane becomes `activeTab`. Focus mode, entered from elsewhere, takes the focused pane as `focusProfile`.

## Persistence

`layout.json` (schema in doc 07), written atomically (temp file + `MoveFileEx(REPLACE_EXISTING)`) at most once every 500 ms after the last change, and on exit. The window rectangle is validated against the current monitors on load; a window that would be off-screen is moved to the nearest monitor's work area, and a monitor id that no longer exists falls back to the primary.

## Transitions and motion

The shell animates from the previous rectangles to the new ones over 150 ms (ease-out) by interpolating rectangles per frame and issuing one `DeferWindowPos` batch for all hosted windows per frame. With Windows animations off, it jumps. During a drag the dragged pane's hosted window is hidden and a WPF stand-in is drawn, so the Chromium surface is never resized 60 times a second.

## Tests (doc 10)

Table-driven: for each mode and n = 1..9 at 1366×768, 1920×1080, 2560×1440, assert rectangles tile `R` exactly (no gaps other than `g`, no overlaps, sums equal), minimums are respected or `tooSmall` is set, and fractions round-trip through JSON. Reorder, hide and resize have property tests: order is a permutation, fractions sum to 1 ± 1e-9.
