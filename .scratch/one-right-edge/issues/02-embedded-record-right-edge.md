# 02: Keep an embedded record on the cell's right edge

**What to build:** A person opens a record table inside a value cell and sees that embedded table's right edge on the cell's right edge. Dragging the edge wider or narrower changes only the embedded table's value side. The name side keeps its width. Collapse puts the host width back to what it was before the open, and the next open grows the host from the value side that was dragged.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Opening the cell places the embedded record's right edge on the value cell's right edge. Sibling fields stay closed, and every value cell in the host record shares the new value-side width.
- [ ] Dragging that edge wider grows the embedded value side by the same amount. The name side stays at its remembered width, and the two right edges still meet.
- [ ] Dragging that edge narrower shrinks the embedded value side. The embedded table is not left clipped with its old value-side width.
- [ ] Setting the host value-side width and setting the embedded value-side width produce the same surface when those edges meet.
- [ ] With the embedded name side at the default 200px, the host value side stops at 256px: the 24px expand control, plus 200px, plus 32px. The name side is not shrunk by that drag.
- [ ] A host value side that was already wider than the embedded record stays that wide. The extra shows as a longer value side and is not remembered as that table's own value-side width.
- [ ] A host that starts at the default 200px, opens, and is dragged to 600px returns to 200px on collapse and opens at 600px again.
- [ ] A host that was already 3000px before the open, then dragged to 500px while open, shows 500px with the edges still meeting. Collapse returns it to 3000px. The next open fills the value side out to 3000px and does not keep 500px.
- [ ] A collapsed scalar on that value side shows more text when the side is wider and less when it is narrower. A full value on that side wraps to the new width.
- [ ] The surface's content width includes the new right edge. One scroll surface is reported. The file is unchanged.
- [ ] A JSON record table follows the same edge.
