# 01: Align a record table's names and values

**What to build:** A record table shows each field name and its value in two aligned cells. Names share one width and values share another. Both start at 200px. Text that does not fit is cut off, and the window shows an ellipsis. Clicking the field name or the value expands or collapses that field. A list table stays as it is today.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Field names in one record table share one horizontal position and one width.
- [x] Values in that record table share one horizontal position and one width.
- [x] Both widths start at 200px.
- [x] A value shorter than the width is shown in full and is not marked cut off.
- [x] A longer value shows the first 25 characters, using the session's 8px advance, and is marked cut off. The window draws an ellipsis and does not paint into the next cell.
- [x] A long field name is cut off the same way and does not cover the value.
- [x] Clicking the field name or its value expands or collapses that field.
- [x] `@` names, prefixed names, and `#text` keep their spelling.
- [x] A list table is still shown as it is today, and opening the document does not change the file.
- [x] Session surface tests cover the shared positions, the 25-character cut-off, and a short value. The window shows the clipped record table.
