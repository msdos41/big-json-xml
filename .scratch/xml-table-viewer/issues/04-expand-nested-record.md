# 04: Expand a nested record inside the cell

**What to build:** A child that occurs once and has fields of its own can be expanded inside its cell. The row grows to show that record table. Collapsing it returns the row to one line. The document still has a single scrollable surface.

**Blocked by:** 01: Open a document as a record table

**Status:** resolved

- [ ] A once-only child that itself has fields shows an expand control. A once-only child with only scalar character data does not.
- [ ] Expanding that cell draws a record table inside the cell and grows the row that owns it.
- [ ] The embedded table's fields follow the same record-table rules as the document element.
- [ ] Collapsing the cell removes the embedded table and returns the row to a single line.
- [ ] Several such cells can be expanded at the same time.
- [ ] The embedded table does not add its own scrollbar. The document remains one scrollable surface, vertical and horizontal.
- [ ] Viewer session tests cover expand, collapse, and two cells open at once, and the window shows the grown row.
