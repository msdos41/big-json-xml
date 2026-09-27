# 05: Widen a cell while it is open

**What to build:** Opening a cell widens its list column, or the record table's value side, enough to show the embedded table. Dragging that width narrower while the cell is open clips the embedded table and keeps the narrower width. Closing the last open cell on that width restores the width from before the open.

**Blocked by:** 03: Drag a table width for this opening; 04: Keep an embedded table inside the opened cell

**Status:** resolved

- [ ] Opening a list cell widens that column to the embedded list's outer width: its index track plus its columns at their current widths.
- [ ] Opening a cell on a record table widens the shared value side to the embedded table's outer width. An embedded record's outer width is its name side plus its value side.
- [ ] Every value cell on that record table becomes wider. Fields that were collapsed stay collapsed.
- [ ] Two open cells in the same column make the column as wide as the wider embedded table.
- [ ] Collapsing one of them leaves enough width for the cell that remains open.
- [ ] Dragging the width narrower while a cell is open clips the embedded table to the cell. The embedded table does not gain its own scrollbar.
- [ ] That width does not jump wide again while the cell stays open.
- [ ] A drag while a cell is open does not replace the stored width. Closing the last cell that contributes to that width restores the stored width.
- [ ] Opening the cell again widens it again.
- [ ] A drag while nothing on that width is open updates the stored width, and collapse restores that stored width.
- [ ] An embedded table's own widths are stored for that embedded table. Dragging them does not rewrite the parent table's widths.
- [ ] The document still has one vertical scrollbar and one horizontal scrollbar.
- [ ] Session surface tests cover automatic widening, the wider of two open cells, a narrowing drag, restore on collapse, and a nested table's own widths.
