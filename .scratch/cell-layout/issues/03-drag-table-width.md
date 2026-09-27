# 03: Drag a table width for this opening

**What to build:** The person can drag the edge of a list column, or the edge between a record table's names and its values. The width lasts for this opening of the document and belongs only to that table. Opening the document again restores the default. A drag stops while the header is still readable.

**Blocked by:** 02: Align a list table under its column headers

**Status:** resolved

- [ ] Dragging the edge of a list column changes that column's width. The header and the entries stay aligned.
- [ ] Dragging the edge between names and values changes the name side, the value side, or both. Every field in that record table follows.
- [ ] A width cannot be dragged below 32px. A header longer than the width is cut off, and the header text does not force the width to stay open.
- [ ] Dragging a field column does not resize the entry index track.
- [ ] Clicking a column header does not expand a cell. Clicking the entry index does not expand a cell.
- [ ] A width set while nothing in it is open is kept for that table for the rest of this opening, including after the table scrolls out of view.
- [ ] Another table with the same field names keeps its own widths.
- [ ] Opening the document again starts every width at 200px.
- [ ] The document's horizontal range includes a column that was widened, so that column can be scrolled into view.
- [ ] Dragging does not change the file.
- [ ] Session surface tests cover the new width, the 32px floor, per-table memory, and a fresh open. The window performs the drag and shows the new width.
