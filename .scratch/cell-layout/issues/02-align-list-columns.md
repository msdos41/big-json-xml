# 02: Align a list table under its column headers

**What to build:** A list table shows each field in its own cell under a column header. A missing field leaves an empty cell. A field seen later becomes a column at the end. The entry index sits in a fixed narrow track. The fields of an entry are not joined with `|`.

**Blocked by:** 01: Align a record table's names and values

**Status:** ready-for-agent

- [ ] Column headers and the entry cells under them share one horizontal position and one width per column.
- [ ] Each column starts at 200px.
- [ ] A missing field occupies an empty cell, and the columns after it stay in place.
- [ ] A field first seen on a later entry is appended as the last column while the scan is still running. Columns already shown keep their places.
- [ ] The entry index is shown in a narrow track beside the columns. That track is not a field column.
- [ ] An entry is not one string joined with `|`. The text of a one-field entry is the text of that cell.
- [ ] A long scalar in a default-width column is cut off in the same way as a long value on a record table.
- [ ] `#text`, `@` names, and prefixed names keep their spelling.
- [ ] Session surface tests cover alignment, an empty cell, an appended column, and the index track. The window smoke test reads a one-field entry from its cell.
