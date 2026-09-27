# 06: Show a scalar up to the cell width

**What to build:** A collapsed scalar is one line. It shows as much of the value as the cell is wide, up to the whole value. Dragging the cell wider reveals the rest. Expanding the cell wraps the full value inside the cell and grows the row. A search hit shows a slice that contains the match and fills the cell, without opening the full value.

**Blocked by:** 03: Drag a table width for this opening

**Status:** resolved

- [ ] At the default 200px width, a collapsed scalar shows 25 characters, or the whole value when it is shorter. A longer value is marked cut off.
- [ ] The text on the surface and the text reported for that scalar are the same. The old 200-character window no longer limits either one.
- [ ] Dragging the width wider shows further characters. When the width holds the whole value, the whole value is shown and is not marked cut off.
- [ ] With no hit, the line starts at the beginning of the value.
- [ ] Revealing a hit shows a slice that contains the match and fills the cell width. The full value stays collapsed.
- [ ] A scalar that does not fit can be expanded. The full value wraps to the cell width, and the row grows on the same scroll surface.
- [ ] Widening the cell while the full value is open rewraps the text and shortens the row. Narrowing it rewraps the text and grows the row.
- [ ] Collapsing the full value returns the one-line cell.
- [ ] A scalar that already fits has no full-value control.
- [ ] Opening a list does not expand full values inside it.
- [ ] Decoding a slice far into a multi-megabyte value does not allocate the whole value. Only the lines that meet the viewport are decoded.
- [ ] Session surface tests cover the default cut-off, a wider cell, a hit, rewrap, and the allocation bound. The window shows an ellipsis until the value fits.
