# 03: Keep an embedded list on the column's right edge

**What to build:** A person opens a list table inside a cell and sees that list's right edge on the cell's right edge. Dragging the edge changes the list's last field column. Earlier field columns and the entry index track keep their widths. Collapse restores the host column, and the next open grows it from the remembered last field column.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Opening the cell places the embedded list's right edge on the host column's right edge. The last field column is the side that meets it.
- [x] Dragging that edge wider or narrower changes the last field column. Earlier field columns keep their remembered widths, and the entry index track keeps its own width.
- [x] Setting the host column width and setting the last field column width produce the same surface when those edges meet.
- [x] A list with one field column treats that column as the flexible side. The host column stops at the 24px expand control, plus the index track, plus 32px.
- [x] A host column that starts at its width from before the open, is dragged while the list is open, returns to that earlier width on collapse, and grows from the remembered last field column on the next open when that column makes the list wider.
- [x] A collapsed scalar in the last field column shows as much text as the new width holds.
- [x] The surface's content width includes the new right edge. One scroll surface is reported. The file is unchanged.
- [x] A JSON list table follows the same edge.
