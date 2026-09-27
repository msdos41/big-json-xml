# 04: Keep a closed record as an outline

**What to build:** A closed record keeps an outline and no second parsed copy of that element. Opening it shows the fields the scan already learned. A list inside it uses the stored count, columns, and entry places, so opening the record does not scan that list again. A document of many distinctly named elements does not keep a resident body for each closed element, and opening one nested record costs work for that record.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] After a record is closed, opening it shows the same field names and kinds the scan learned, without a second parsed copy of that element.
- [x] Collapsing that record keeps the outline. Opening it again does not walk the rest of the document.
- [x] A list field inside the record uses the stored count, columns, and entry places. Opening the record does not scan that list again.
- [x] A document of many distinctly named elements retains the outline, not a resident body per closed element.
- [x] Opening one nested record in that document takes work proportional to that record, not to the whole document.
- [x] An element the scan is still inside can appear as a field before its end tag.
- [x] The checks run on the Viewer session. The about-ten-second bar for a 500MB document is a later ticket.
