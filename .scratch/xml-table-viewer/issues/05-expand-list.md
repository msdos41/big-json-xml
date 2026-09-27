# 05: Expand a list inside the cell

**What to build:** Expanding a list field draws that list table inside the cell. Each entry is one row, and its fields are columns. Collapsing the cell returns the `Name[n]` label. Scrolling the list uses the document's one scroll surface.

**Blocked by:** 03: Show list fields and live counts; 04: Expand a nested record inside the cell

**Status:** resolved

- [ ] Expanding a list field embeds a list table in that cell and grows the row.
- [ ] Field names are column headers. One entry is one row. The row is not also drawn as a nested record table of the same fields.
- [ ] Columns already shown keep their places. A field discovered on a later entry is appended.
- [ ] A once-only nested child inside an entry can still be expanded as a record table in that entry's cell.
- [ ] Collapsing the list cell removes the embedded list and restores `Name[n]`.
- [ ] Scrolling the embedded list does not add a scrollbar and does not create a control for every entry. Only the visible slice is realized.
- [ ] Viewer session tests cover column append, collapse, and a slice of a list longer than the viewport, and the window shows the embedded list.
