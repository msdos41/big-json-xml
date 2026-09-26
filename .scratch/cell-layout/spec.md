# Cell layout

Status: ready-for-agent

## Problem Statement

A person inspecting a document sees list tables as one line of text with fields separated by `|`. The columns do not line up. Record tables are two runs of text, so a long field name runs into the value. Expanding a cell lays the embedded table out under the whole entry, across the other fields, instead of inside the cell that was opened. A scalar stops at 200 characters even when the person makes that cell wider.

## Solution

The same viewer draws each field in its own cell, aligned with the other cells in that table. A list table's column header and every entry share one width per column. A record table's field names share one width and its values share another. The person can drag those widths. Opening a cell shows the embedded table or the full value inside that cell only. A collapsed scalar shows as much of the value as the cell is wide, up to the whole value. The document still has one vertical scrollbar and one horizontal scrollbar.

## User Stories

1. As a person inspecting a document, I want each field drawn in its own cell, so that I can read across a row without `|` between the fields.
2. As a person inspecting a document, I want a record table's field names to share one width, so that the names line up down the table.
3. As a person inspecting a document, I want a record table's values to share one width, so that the values line up down the table.
4. As a person inspecting a document, I want a long field name cut off inside the name width, so that it does not cover the value.
5. As a person inspecting a document, I want a long value cut off inside the value width, so that it does not cover the next field.
6. As a person inspecting a document, I want a cut-off name or value to show an ellipsis, so that I can tell there is more text.
7. As a person inspecting a document, I want a list table's column headers and entries to share the same column widths, so that a value sits under its header.
8. As a person inspecting a document, I want an entry's index in its own narrow track beside the columns, so that I can tell the entries apart without that number taking a field column.
9. As a person inspecting a document, I want a missing field to keep an empty cell in its column, so that the next field does not slide left.
10. As a person inspecting a document, I want a field discovered later to be added as a column at the end, so that columns I am already looking at stay put.
11. As a person inspecting a document, I want that new column to appear while the scan is still running, so that I do not wait for the end of the list to see the table.
12. As a person inspecting a document, I want each column to start at a capped default width, so that one long value does not open the table at the full width of the text.
13. As a person inspecting a document, I want to drag the edge of a list column, so that I can give a chatty field more room.
14. As a person inspecting a document, I want to drag the edge between a record table's names and its values, so that I can widen either side.
15. As a person inspecting a document, I want a drag to stop while the header is still readable, so that I cannot drag a column away.
16. As a person inspecting a document, I want a header longer than that minimum to be cut off, so that a long name does not force the column to stay wide.
17. As a person inspecting a document, I want a width I set while the cell is collapsed to last for this opening of the document, so that scrolling away does not reset it.
18. As a person inspecting a document, I want that width to belong only to that table, so that another list with the same field names keeps its own widths.
19. As a person inspecting a document, I want widths to return to the default when I open the document again, so that a previous viewing does not rearrange this one.
20. As a person inspecting a document, I want the entry index track to stay fixed, so that dragging a field column does not resize the index.
21. As a person inspecting a document, I want clicking a cell to expand or collapse that cell, so that the cell I aimed at is the one that changes.
22. As a person inspecting a document, I want a second cell on the same entry to stay as it was, so that opening `items` does not open or close `address`.
23. As a person inspecting a document, I want the embedded table drawn inside the opened cell, so that it does not spread under the other fields of that entry.
24. As a person inspecting a document, I want the entry to be as tall as its tallest open cell, so that both embedded tables fit.
25. As a person inspecting a document, I want the other cells on that entry to stay at the top of the row, so that their text does not drop to the bottom of a tall neighbor.
26. As a person inspecting a document, I want the next entry to start below that tall row, so that an embedded table does not cover the following entry.
27. As a person inspecting a document, I want collapsing one cell to leave the other open, so that each cell is independent.
28. As a person inspecting a document, I want the row to return to a single line only when every cell in it is collapsed, so that a remaining embedded table is still visible.
29. As a person inspecting a document, I want opening a list cell to widen that column to the embedded list's default width, so that I can see the embedded columns without dragging first.
30. As a person inspecting a document, I want opening a cell on a record table to widen the shared value side, so that the embedded table fits in the value.
31. As a person inspecting a document, I want that wider value side to widen every value cell, so that the values still line up.
32. As a person inspecting a document, I want those other fields to stay collapsed, so that a wider value is not the same thing as opening the field.
33. As a person inspecting a document, I want a record field's row to grow only for the field I opened, so that `@id` stays one line when `items` is open.
34. As a person inspecting a document, I want two open cells in the same column to make that column wide enough for the wider embedded table, so that neither one is clipped just because the other exists.
35. As a person inspecting a document, I want collapsing one of those cells to keep enough width for the cell that is still open, so that the remaining embedded table stays readable.
36. As a person inspecting a document, I want narrowing a width while a cell is open to clip the embedded table to the cell, so that my drag is respected.
37. As a person inspecting a document, I want that narrowed width not to jump wide again while the cell stays open, so that the table does not fight the drag.
38. As a person inspecting a document, I want collapsing the last open cell of that width to restore the width from before the open, so that a temporary widening goes away with the embedded table.
39. As a person inspecting a document, I want opening that cell again to widen it again, so that each open starts by showing the embedded table.
40. As a person inspecting a document, I want a drag while nothing in that width is open to be the width I come back to, so that my collapsed layout is what collapse restores.
41. As a person inspecting a document, I want an embedded table's own columns to remember their own widths, so that dragging inside the cell does not rewrite the parent table.
42. As a person inspecting a document, I want a clipped embedded table to stay on the document's scrollbars, so that nested cells do not each gain a scrollbar.
43. As a person inspecting a document, I want a list column header to scroll away with the list, so that a long list does not pin a stack of headers to the viewport.
44. As a person inspecting a document, I want the header and the entries to move together horizontally, so that a header stays above its column while I scroll sideways.
45. As a person inspecting a document, I want a collapsed scalar on one line, so that a value does not wrap until I ask for the full value.
46. As a person inspecting a document, I want that line to show as many characters as the cell is wide, so that dragging wider reveals the rest of the value.
47. As a person inspecting a document, I want the whole value on that line when the cell is wide enough, so that there is no 200-character ceiling.
48. As a person inspecting a document, I want the start of the value when I did not arrive from a hit, so that the preview is stable.
49. As a person inspecting a document, I want a hit's cell to show a slice that contains the match and fills the cell width, so that I see the matching words and as much neighborhood as fits.
50. As a person inspecting a document, I want revealing a hit not to expand the full value by itself, so that a huge text does not suddenly become as tall as the whole value.
51. As a person inspecting a document, I want to expand a scalar that does not fit, so that I can read the full value without dragging the cell to the full string width.
52. As a person inspecting a document, I want that full value wrapped to the cell width, so that the text stays inside the cell.
53. As a person inspecting a document, I want the row to grow to the wrapped full value, so that I can scroll through it on the same surface.
54. As a person inspecting a document, I want widening the cell while the full value is open to rewrap and shorten the row, so that the text follows the width I just set.
55. As a person inspecting a document, I want narrowing it to rewrap and grow the row, so that nothing spills into the next cell.
56. As a person inspecting a document, I want collapsing the full value to return to the one-line cell, so that the row can become short again.
57. As a person inspecting a document, I want a short scalar that already fits to have no full-value control, so that there is nothing further to open.
58. As a person inspecting a document, I want only the visible slice of a full value to be decoded, so that a multi-megabyte text is not turned into one string to paint a slice.
59. As a person inspecting a document, I want opening a list not to expand full values inside it, so that long text stays collapsed until I open that cell.
60. As a person inspecting a document, I want only the visible slice of an expanded list to be realized, so that a million entries do not create a million cells.
61. As a person inspecting a document, I want the scroll range to include the virtual height of an open cell, so that I can scroll through an embedded table that is taller than the window.
62. As a person inspecting a document, I want the document's horizontal range to include a column I widened, so that I can scroll to fields that are past the window.
63. As a person inspecting a document, I want clicking a column header to do nothing, so that the header is not mistaken for an entry cell.
64. As a person inspecting a document, I want clicking the entry index to do nothing, so that the index is not mistaken for a field.
65. As a person inspecting a document, I want clicking a record table's field name or its value to toggle that field, so that the one cell on that row is what opens.
66. As a person inspecting a document, I want a marked hit to stay marked inside its cell, so that I can still see which match I revealed.
67. As a person inspecting a document, I want `#text`, `@` names, and prefixed names to keep their current spelling inside the new cells, so that aligning the table does not rename fields.
68. As a person inspecting a document, I want the document to stay read-only, so that dragging a width cannot change the file.

## Implementation Decisions

- Stay on the custom virtualized surface. DataGrid, TreeDataGrid, and a different desktop framework are not part of this work. ADR-0005, ADR-0009, ADR-0015, ADR-0022, and ADR-0023 stand.
- The viewer session's surface is the only layout authority. The desktop shell paints that surface and turns a pointer position into a session command. It does not measure text to decide widths, and it does not lay embedded tables out a second time.
- The surface describes every visible field as a cell: horizontal position, top, width, height, visible text, whether the text is cut off, the path, and whether that cell is expanded. A list entry is no longer one string joined with `|`. A column header uses the same horizontal positions and widths as the entry cells. The logical list read used by search and by "read this slice of entries" stays free of pixel geometry.
- A record table has two widths, the field-name side and the value side. They are not columns. A list table has one width per field column, in first-seen order, plus a fixed index track wide enough for the entry number. The index track is not a field column and is not dragged.
- The default width of a field column, of the field-name side, and of the value side is 200px. The session already maps width to characters with a fixed 8px advance. Tests use that advance. At the default width a collapsed scalar therefore shows 25 characters, or the whole value when the value is shorter. There is no remaining 200-character ceiling.
- The minimum width is four advances, 32px, unless the header itself is shorter. A longer header is cut off. The header does not force the width to the full header string.
- Each width is stored for that table's path for the session. A list column is keyed by that list and that field. A record table stores its name-side width and its value-side width on that record's path. Reopening the document drops the stored widths. Two tables do not share a width because their field names match.
- A drag while no open cell is contributing to that width updates the stored width. A drag while an open cell is contributing changes only the width currently shown and suppresses further automatic growth until the last contributing cell collapses. Collapse of that last cell restores the stored width. Opening again may widen automatically again.
- Automatic width is the maximum of the stored width and the outer width of each embedded table currently open in that width. The outer width of an embedded list is its index track plus its columns at their current widths. The outer width of an embedded record is its name side plus its value side. A user narrow during that open replaces this maximum until the last contributing cell collapses.
- An expanded cell's rectangle contains its embedded table, including that table's headers, and does not also keep the collapsed `Name[n]` label. Sibling cells on a list entry keep their own rectangles, top-aligned. The next entry begins at the bottom of the tall entry. On a record table only the opened field's row grows; other rows stay one line tall and their value cells use the shared value width.
- Virtualization may still describe an embedded table as slices, but every slice is positioned inside the owning cell and only slices that meet the viewport are realized. The entry's height includes the full virtual height. The document reports one scroll surface. Content width is the right edge of the widest table. Content height includes open cells.
- A collapsed scalar cell shows one line: from the start of the value, or a window that contains a revealed hit and fills the cell, for as many characters as the advance allows, up to the whole value. The surface reports that text and whether it is cut off. The desktop draws the ellipsis when the surface says the text is cut off, and clips to the cell so glyphs cannot enter the next cell.
- Full value remains a separate expansion of that scalar cell. It wraps to the cell width using the same advance. The row's virtual height is the wrapped height. Only lines that meet the viewport are decoded. Changing the width while the full value is open rewraps. A scalar whose entire value already fits the one line has no full-value control. Opening a list does not expand full values inside it. Revealing a hit does not expand the full value.
- Clicking a list entry cell toggles that cell's path. Clicking the index track or a column header does not. Clicking a record field's name or its value toggles that field. The desktop hit test uses the cell rectangles from the surface.

## Testing Decisions

- Test external behavior on the viewer session surface: cell rectangles, visible text, cut-off, which path is expanded, row height, content width, content height, and that only one scroll surface is reported. Do not assert paint calls, fonts, or pixels.
- A good test opens a document, expands or collapses a cell, sets a width, moves the viewport, and reads the surface. It does not care how rows are stored internally.
- Cover a list whose fields align under their headers, an empty cell that keeps its column, a column appended at the end, a record table whose values share one width, two cells open on one entry, collapse of only one of them, automatic widening, a drag that clips and is restored on collapse, a width that is not shared with another table of the same name, a scalar that grows from 25 characters to the whole value as the width grows, a full value that rewraps, a hit window that fills the cell and does not expand the full value, and a viewport that realizes a slice of a tall embedded table without a second scroll surface.
- The existing session tests that expand a list, jump to a slice, and open a scalar window are the prior art. The scalar test that expects a 200-character window is updated to the width-based count. The desktop smoke test that opens, expands, searches, and reveals a hit is updated so a one-field entry is read from its cell. Column geometry is not specified in the desktop test.
- A huge scalar still has a test that decoding a slice far into the value does not allocate the whole value.

## Out of Scope

- Replacing the viewer surface with DataGrid, TreeDataGrid, or another desktop framework.
- Saving widths on disk, or sharing one width across every field of the same name.
- Pinning column headers to the viewport.
- A scrollbar inside a cell or inside an embedded table.
- Virtualizing columns when a table has tens of columns.
- Editing or saving the document, and opening JSON.
- Changing which siblings form a list, how search queries are written, or how hits are ordered.
- Golden images of the painted window.

## Further Notes

- ADR-0022 and ADR-0023 record why the embedded table widens its own cell and why a scalar follows that width. ADR-0019's 200-character ceiling does not apply. ADR-0015's "collapse returns the row to a single line" applies only when no cell in the row remains open.
- Where this disagrees with the XML table viewer spec, this spec wins. That includes the stories that cap a scalar at 200 characters, and the story that collapsing a cell always returns the row to a single line.
- "Column" in this spec means a list table column. A record table has a name side and a value side.
