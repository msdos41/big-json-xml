# One right edge

Status: ready-for-agent

## Problem Statement

A person opens a cell and sees an embedded table whose right edge starts on that cell's right edge. Dragging the edge to make the cell wider leaves a gap inside the cell: the embedded table's last flexible side stays where it was. Dragging the edge narrower clips that table, so its last flexible side can disappear even though a width is supposed to keep 32px of text. After either drag, opening a table inside that embedded table does not push the parent cell, or the cell above that, wide enough to show the new table. A list column whose header is one character can also be dragged down to a single character width.

## Solution

The embedded table's right edge stays on the cell's right edge. Dragging that edge wider or narrower changes the flexible side of every embedded table that ends on it, at every depth, and moves every cell that shares the width. A record table's flexible side is its value side. A list table's flexible side is its last field column. Dragging a divider to the left of that edge changes only that column, and every vertical line to its right moves by the same amount. Opening a deeper embedded table still grows every ancestor when the new table needs more room, and does not shrink a width that is already wider. A drag stops at 32px, or higher when an open embedded table's other columns, index track, and expand control must still fit. Collapse puts the host width back to what it was before the open. Widths on the embedded table stay for this opening.

## User Stories

1. As a person inspecting a document, I want an embedded table's right edge to sit on its cell's right edge when I open the cell, so that I do not see a gap or a clipped table.
2. As a person inspecting a document, I want a record table's value side to be the side that meets that edge, so that the field names keep the width they already have.
3. As a person inspecting a document, I want a list table's last field column to be the side that meets that edge, so that earlier field columns keep their widths.
4. As a person inspecting a document, I want the entry index track to keep its own width, so that dragging a field column does not resize the index.
5. As a person inspecting a document, I want the same edge rule for an XML document and a JSON document, so that the grammar does not change how a table resizes.
6. As a person inspecting a document, I want every value cell in a record table to share one value side, so that dragging one value's edge moves the other values with it.
7. As a person inspecting a document, I want every entry cell in a list column to share that column's width, so that the header and the entries stay aligned.
8. As a person inspecting a document, I want opening one cell to leave its sibling cells closed, so that I only expand the field I aimed at.
9. As a person inspecting a document, I want a host width that was already wider than the new embedded table to stay that wide, so that opening a small table does not shrink the other cells on that width.
10. As a person inspecting a document, I want the extra room in that wider cell to show as a longer flexible side, so that the right edges still meet.
11. As a person inspecting a document, I want that extra length not to be remembered as the embedded table's own width, so that it goes away when the host is no longer holding the cell open.
12. As a person inspecting a document, I want two open cells on one width to share one right edge, so that the narrower embedded table meets the wider one.
13. As a person inspecting a document, I want the narrower table's fill not to be remembered, so that closing the wider cell lets the width settle on the narrower table's own outer width.
14. As a person inspecting a document, I want the shared width to be at least the wider embedded table, so that neither open table is clipped.
15. As a person inspecting a document, I want to drag the shared right edge wider, so that the flexible side grows and the two edges stay one line.
16. As a person inspecting a document, I want to drag that edge narrower, so that the flexible side shrinks and the table is not clipped in place.
17. As a person inspecting a document, I want columns to the left of that edge to keep their remembered widths, so that only the flexible side absorbs the drag.
18. As a person inspecting a document, I want every deeper embedded table on that same edge to take the new flexible width, so that a grandchild table meets the line I dragged.
19. As a person inspecting a document, I want those deeper widths to be remembered, so that the drag is still there while the cell stays open.
20. As a person inspecting a document, I want a wider embedded table in another cell of the same width to take the same new edge, so that one drag moves every cell on that line.
21. As a person inspecting a document, I want that other table's new flexible width to be remembered, so that it does not spring back while the cell stays open.
22. As a person inspecting a document, I want a right-edge drag to leave every table on that edge meeting it exactly, so that none of them is only filled on screen after the drag.
23. As a person inspecting a document, I want dragging the cell's edge and dragging the embedded table's edge to be the same change, so that either grab gives the same widths.
24. As a person inspecting a document, I want a collapsed scalar on that width to show as much text as the new width holds, so that a wider cell reveals more of the value and a narrower cell shows less.
25. As a person inspecting a document, I want a full value on that width to wrap again to the new width, so that the row height follows the cell.
26. As a person inspecting a document, I want the document's horizontal range to include the new right edge, so that I can scroll the widened table into view.
27. As a person inspecting a document, I want no maximum on a document-table width, so that I can keep dragging outward.
28. As a person inspecting a document, I want a right-edge drag to stop at 32px for a plain cell, so that four character widths remain visible.
29. As a person inspecting a document, I want a one-character list header to stop at 32px too, so that a short header cannot be dragged down to one character.
30. As a person inspecting a document, I want a header longer than 32px to be cut off, so that a long name does not force the column to stay at the full header.
31. As a person inspecting a document, I want the floor to include an open embedded table's other columns at their current widths, so that those columns are not crushed by this drag.
32. As a person inspecting a document, I want that floor to include the list's index track and the expand control, so that the index and the expand button still fit.
33. As a person inspecting a document, I want a nested open table to raise the floor again, so that a grandchild's other columns count before the outer edge can move.
34. As a person inspecting a document, I want the strictest cell on the edge to set the floor, so that one tight cell stops the drag for every cell that shares the edge.
35. As a person inspecting a document, I want the drag to stop there without shrinking the columns it did not change, so that I narrow those columns by dragging their own dividers.
36. As a person inspecting a document, I want a record whose name side is still 200px to keep the outer value side at least the expand control plus 200px plus 32px, so that the names stay readable while I drag the outer edge.
37. As a person inspecting a document, I want a single-column list to treat that column as the flexible side, so that the outer edge still resizes it down to the expand control plus the index track plus 32px.
38. As a person inspecting a document, I want to drag the divider between a record table's names and its values, so that only the name side changes width.
39. As a person inspecting a document, I want that drag to move every vertical line to the right by the same amount, so that the embedded table, its host cell, and each ancestor stay aligned.
40. As a person inspecting a document, I want the value side's remembered width to stay put during that drag, so that the names take the change and the values do not trade width with them.
41. As a person inspecting a document, I want the name side to stop at 32px, so that the field names keep four character widths.
42. As a person inspecting a document, I want the value side's floor not to block that name-side drag, so that I can narrow the names while the values keep their width.
43. As a person inspecting a document, I want a drag of a list column that is not the last to change that column only, so that columns to its right keep their widths and shift.
44. As a person inspecting a document, I want a scalar in a column to the right of that divider to show the same amount of text, so that a shift does not rewrap it.
45. As a person inspecting a document, I want a divider that is itself an embedded table's right edge to resize that table's flexible side, so that the table inside the column I dragged stays on the new edge.
46. As a person inspecting a document, I want that inner flexible width to be remembered, so that the column drag is kept on the inner table.
47. As a person inspecting a document, I want a left-divider drag in a narrower cell to pull the shared edge with it, so that the wider cell's right edge moves by the same amount.
48. As a person inspecting a document, I want the wider cell's flexible side to be set to that new edge and remembered, so that it does not stay behind at the old width.
49. As a person inspecting a document, I want that drag to stop when the wider cell is at its floor, so that I cannot pull another cell's text away.
50. As a person inspecting a document, I want a left-divider drag to move the edge even when the width from before the open was wider, so that the lines move while the cell is open.
51. As a person inspecting a document, I want opening a deeper embedded table to grow this width when the new table needs more room, so that the new table is not clipped.
52. As a person inspecting a document, I want that growth to continue through every ancestor, so that a parent and a grandparent cell both open up.
53. As a person inspecting a document, I want that growth not to shrink a width that is already wider, so that a previous wider drag stays.
54. As a person inspecting a document, I want a neighbor stretched by that growth to fill on screen only, so that closing the new table lets the neighbor return to its own width.
55. As a person inspecting a document, I want opening a deeper table inside a column that is not last to widen that column, shift the columns to its right, and grow the ancestors, so that the last field column's remembered width stays.
56. As a person inspecting a document, I want growing a record's value side to widen every value cell in that record, so that the values stay aligned, and I do not want those other fields to expand.
57. As a person inspecting a document, I want collapsing the deeper table to return the host to the width from before that deeper open, so that a temporary growth does not stick.
58. As a person inspecting a document, I want a drag made before that deeper open to still be there after the deeper table collapses, so that the drag and the temporary growth are different.
59. As a person inspecting a document, I want collapsing the last open cell on a width to restore the host width from before any of those cells were open, so that the open does not permanently change the host.
60. As a person inspecting a document, I want collapsing one of two open cells to leave the width at the remaining open table, so that the closed cell is the only one that goes away.
61. As a person inspecting a document, I want the embedded table to keep the column widths I dragged, so that the next open can grow the host from them.
62. As a person inspecting a document, I want a host that started at the default 200px, was opened, and was dragged to 600px, to return to 200px when I collapse it and to open at 600px again, so that the embedded table's drag is what grows it the next time.
63. As a person inspecting a document, I want a host that was already 3000px before the open, then dragged to 500px while open, to return to 3000px on collapse, so that a during-open narrowing does not replace the width from before the open.
64. As a person inspecting a document, I want the next open of that 3000px host to fill the flexible side out to 3000px again, so that the during-open 500px does not remain.
65. As a person inspecting a document, I want a width to belong only to that table, so that another table with the same field names keeps its own widths.
66. As a person inspecting a document, I want a width to remain when its table scrolls out of view, so that scrolling back shows the same edges.
67. As a person inspecting a document, I want rows that are not on screen to use the same width, so that scrolling does not reveal a different edge.
68. As a person inspecting a document, I want opening the document again to restore every width to the default, so that this opening does not rearrange the next one.
69. As a person inspecting a document, I want another open document to keep its own widths, so that dragging in one document does not resize the other.
70. As a person inspecting a document, I want the default of each side to stay capped, so that a long value or a wide table does not start at the full width of its text until I open or drag.
71. As a person inspecting a document, I want the document to keep one vertical scrollbar and one horizontal scrollbar, so that an embedded table does not gain its own.
72. As a person inspecting a document, I want a drag not to change the file, so that inspecting stays read-only.
73. As a person inspecting a document, I want a cut-off header or scalar to show that it is cut off, so that I can tell there is more text than the width holds.

## Implementation Decisions

- The behavior lives in the viewer session, which already stores one width for a record table's name side, one for its value side, and one per field column of a list table, and already exposes those widths by laying out a surface of cell rectangles. The desktop drag stays a forwarder onto those three operations. No new drag operation is added.
- While a cell is open, setting the host width and setting the flexible side of an embedded table on that edge are one change. Tests may call either operation. The surface is the same.
- Each width remembers two different things. The stored width is what the host shows when no open cell is contributing. A drag while nothing on that width is open updates the stored width. A drag while a cell is open does not replace it. A during-open override is the width the person dragged to while a cell was open. It may be narrower than the stored width. It lasts until the last contributing cell on that width collapses.
- While at least one contributing cell is open, the shown width is the maximum of the during-open override, when one is set, and the outer width of each open embedded table on that width. When no override is set, the shown width is the maximum of the stored width and those outer widths.
- A record table's outer width is its name side plus its value side. A list table's outer width is its index track plus its field columns. The host cell adds the expand control already used for an open cell. That control is 24px. The index track keeps its existing rule: one more than the number of digits in the entry count, times the 8px advance.
- The flexible side of a record table is the value side. The flexible side of a list table is the last field column in scan order. A list with one field column uses that column. The index track is never a flexible side.
- The default of each side stays 200px. The minimum of a resizable side is 32px, four advances. This includes a list column whose header is shorter than 32px. A longer header is cut off. There is no maximum.
- A right-edge drag sets the during-open override to the requested width, raised to the floor, and writes the stored flexible width of every embedded table that ends on that edge, at every depth, so each table's outer width meets the new edge. Columns the drag does not change keep their stored widths. Display-only fill on that edge is committed by the drag.
- A left-divider drag writes the stored width of the column it belongs to and moves the shown width by the resulting delta, including setting an override when that shown width falls below the stored host width. Remembered widths to the right of the divider, on the table being dragged, stay. Every vertical line to the right moves by that delta, including nested tables, the host, and each ancestor. An embedded table in another cell on the moving edge has its flexible side written to the new edge. A divider that is itself an inner embedded table's right edge writes that inner table's flexible side the same way as a right-edge drag.
- The floor of a drag is the strictest floor among the cells that would change. A plain cell's floor is 32px. An open cell whose right edge is the line being dragged has a floor of the expand control, plus the current widths of the columns this drag does not change, plus the index track when the table is a list, plus the floor of the flexible side. The flexible side's floor repeats that rule when it contains a deeper open table. The drag stops at the floor. It does not go on to shrink the other columns. A requested width below the floor is raised to the floor.
- Opening a cell does not write a display-only fill into the flexible side. A later open grows the shown width, and every ancestor, only when the new outer width is larger. It does not write a neighbor's fill. It does not shrink a wider shown width. Collapsing that deeper table removes only the growth it caused. A drag written before that open remains.
- Collapsing the last contributing cell clears the during-open override and shows the stored host width. Collapsing one of several contributing cells leaves the shown width at the maximum of any remaining override and the remaining open tables. Stored widths on an embedded table survive collapse. The next open can grow the host from them when they exceed the stored host width. When the stored host width is still larger, the flexible side fills on screen and that fill is not written.
- A width still belongs to one table for this opening. Another table with the same field names does not share it. Reopening the document restores defaults. Widths apply to rows that are not realized. Another open document is untouched. The surface still reports one scroll surface. Content width is the right edge of the widest table.
- This withdraws the cell-layout rule that a drag while a cell is open only changes the shown width, clips the embedded table, and suppresses later growth. It also withdraws the exception that let a list column shrink to a header shorter than 32px.

## Testing Decisions

- Test external behavior on the viewer session surface only. A good test opens a document, expands or collapses a cell, sets a name-side width, a value-side width, or a list column width, and reads the surface. It asserts cell rectangles, which edges meet, shown text, cut-off, content width, and that one scroll surface is reported. It does not assert paint, pixels, which row received the pointer, or how a width is stored internally.
- The existing session tests that expand a cell, set a width, and read the surface are the prior art. Extend that style. Do not add a desktop seam for this behavior. The window already forwards a drag to the three width operations.
- Cover a record embedded in a record value, a list embedded in a list column, and a grandchild table. Cover a host that is already wider than the embedded table, two open cells of different outer widths on one shared width, a right-edge drag in both directions, a left-divider drag that translates the edges to its right, a deeper open that grows ancestors, collapse of the last cell, collapse of one of two cells, and a during-open narrowing that is narrower than the width from before the open.
- Assert the floors with the existing numbers: 200px default, 32px minimum, 8px advance, 24px expand control, and the existing index-track rule. Include the record case whose name side is 200px, so the outer value side stops at 256px, and one deeper record that raises that floor. Include a one-character list header that stops at 32px.
- Update the existing session test that sets a one-character column below 32px and expects a single advance. It now expects 32px. Replace the existing session test that a drag while open clips the embedded table and refuses to grow again. The shared edge and the later open are the new behavior.
- One XML document carries the edge rules. One JSON record and one JSON list show that the grammar does not pick a different rule. Do not repeat every story in both grammars.
- A fresh session, or opening the document again, shows default widths. A second table with the same field names keeps its own widths.

## Out of Scope

- Saving widths on disk, or sharing one width across tables because their field names match.
- A scrollbar inside a cell or an embedded table, and pinning headers to the viewport.
- Changing the 200px default, the 8px advance, or the 24px expand control.
- Dragging the entry index track, or resizing the found-column in the results window.
- Scaling every column together, or taking the drag out of the flexible side so the outer edge stays still.
- Pixel tests, hit-testing which coincident row receives the pointer, and editing or saving the document.
- Changing which siblings form a list, how a scalar chooses its window, or how a hit is revealed.

## Further Notes

- ADR-0048 records this decision. ADR-0022's pin-and-clip sentence is withdrawn. Session-scoped widths, collapse restoring the host width, capped defaults, and no scrollbar on an embedded table still stand.
- The glossary's embedded table has the cell's right edge as its own right edge. "Column" in this spec means a list table's field column. A record table has a name side and a value side.
- Where this disagrees with the cell-layout spec, this spec wins. That includes a drag while open clipping the embedded table and suppressing later growth, and a list column shrinking to a header shorter than 32px.
