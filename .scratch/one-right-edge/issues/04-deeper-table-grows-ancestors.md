# 04: Grow every ancestor when a deeper table needs room

**What to build:** A person opens an embedded table inside an embedded table. When the new table needs more room, the parent cell and the grandparent cell both grow, and their right edges stay together. The growth does not shrink a width that is already wider. Closing the deeper table removes only the room that table added.

**Blocked by:** 02: Keep an embedded record on the cell's right edge, 03: Keep an embedded list on the column's right edge.

**Status:** resolved

- [x] Opening a grandchild record inside an embedded record grows the parent value side and the grandparent value side when the grandchild's outer width is larger. All three right edges meet.
- [x] That growth does not shrink a value side or field column that is already wider than the new table.
- [x] A neighbor cell stretched only because of this growth fills on screen. Closing the grandchild lets that neighbor return to its own width.
- [x] Closing the grandchild drops the growth it caused. A drag made on the parent before that open is still there.
- [x] Opening a deeper table inside a list column that is not the last widens that column, shifts the field columns to its right without changing their remembered widths, and grows the ancestors. The list's last field column keeps its remembered width.
- [x] Growing a record's value side widens every value cell in that record and does not expand those other fields.
- [x] One scroll surface is reported, and the file is unchanged.
