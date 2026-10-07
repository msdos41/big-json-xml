# 05: Move every right edge from a left divider

**What to build:** A person drags a divider that is not the table's right edge. Only that column changes width. Every vertical line to its right moves by the same amount, including the embedded table, the host cell, and each ancestor. The drag still moves those edges when the width from before the open was wider, and collapse puts that earlier width back.

**Blocked by:** 02: Keep an embedded record on the cell's right edge, 03: Keep an embedded list on the column's right edge.

**Status:** ready-for-agent

- [ ] Dragging the divider between an embedded record's names and values changes only the name side. The value side's remembered width stays. The embedded table's right edge, the host cell's right edge, and each ancestor's right edge move by the same amount.
- [ ] That name side stops at 32px. The value side's floor does not stop the name-side drag while the value side's width is unchanged.
- [ ] Dragging a list field column that is not the last changes that column. Field columns to its right keep their remembered widths and move with the table's right edge. A scalar in one of those right-hand columns shows the same amount of text.
- [ ] A divider that is itself an inner embedded table's right edge sets that table's flexible side to the new width and remembers it.
- [ ] The edges move while the cell is open even when the width from before the open was wider. Collapse restores that earlier width, and the next open fills to it again when it is still the wider one.
- [ ] One scroll surface is reported, and the file is unchanged.
