# 04: Keep an embedded table inside the opened cell

**What to build:** Opening a cell draws its embedded table inside that cell. A sibling cell on the same entry stays as it was and stays at the top of the row. The entry is as tall as its tallest open cell, and the next entry starts below it. On a record table, only the opened field's row grows.

**Blocked by:** 02: Align a list table under its column headers

**Status:** ready-for-agent

- [ ] Clicking one cell of a list entry expands or collapses that cell only.
- [ ] The embedded table, including its own headers, sits inside that cell's rectangle and does not extend across the sibling cells.
- [ ] The open cell does not also show the collapsed `Name[n]` label.
- [ ] A sibling cell on that entry keeps its own text and its own expanded or collapsed state.
- [ ] Sibling cells are top-aligned. The entry is as tall as its tallest open cell. The next entry begins at the bottom of that row.
- [ ] Collapsing one cell leaves another open cell open. The row returns to a single line only when every cell in it is collapsed.
- [ ] Opening a field on a record table grows that field's row only. The other fields stay one line tall.
- [ ] Clicking the field name or the value toggles that record field.
- [ ] Only the slices of an embedded table that meet the viewport are realized. The entry height still includes the full virtual height. The document reports one scroll surface.
- [ ] A list column header scrolls away with the document. It is not pinned to the viewport.
- [ ] A marked hit stays marked on its cell.
- [ ] Session surface tests cover one open cell, two open cells on one entry, collapse of only one, a record field's row, and a viewport slice of a tall embedded list. The window opens the cell that was clicked.
