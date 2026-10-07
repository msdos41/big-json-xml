# 01: Keep every resizable width at least 32

**What to build:** A person inspecting a document cannot drag a list field column, a record table's name side, or a record table's value side narrower than 32px. A one-character header stops at that same width. A longer header is cut off instead of forcing the width open. A document table has no maximum width.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Dragging a one-character list column below 32px leaves it at 32px, and the header and every entry in that column stay aligned.
- [x] Dragging a record table's name side or value side below 32px leaves that side at 32px.
- [x] A header longer than 32px is cut off at that width and does not force the column to stay as wide as the full header.
- [x] A document-table width of several thousand pixels is kept.
- [x] The width belongs to that table for this opening. Another table with the same field names keeps its own width. Opening the document again restores the default.
- [x] The drag does not change the file. The surface still reports one scroll surface.
