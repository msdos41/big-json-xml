# 06: Pull every open cell on a shared edge

**What to build:** Two cells that share a width and are both open share one right edge. The narrower embedded table fills out to the wider one without remembering that fill. Dragging the narrower cell pulls the wider cell to the new edge and remembers the wider cell's new flexible width. The drag stops when the wider cell is at its floor.

**Blocked by:** 02: Keep an embedded record on the cell's right edge, 03: Keep an embedded list on the column's right edge.

**Status:** ready-for-agent

- [ ] Two open cells on one list column, or on one record value side, show one shared right edge. The width is at least the wider embedded table.
- [ ] The narrower table's flexible side fills the gap on screen. That fill is not remembered. Closing the wider cell leaves the width at the narrower table's own outer width.
- [ ] Dragging the narrower cell's right edge, or a left divider inside it, moves the shared edge by that amount. The wider table's flexible side is set to the new edge and remembered.
- [ ] The drag stops when the wider table's flexible side is at its floor, even if the column being dragged is still wider than 32px.
- [ ] Closing one of two open cells leaves the remaining open table at its edge. Closing the last open cell on that width restores the width from before those cells were open.
- [ ] One scroll surface is reported, and the file is unchanged.
