# 07: Show a scalar window, then the full value

**What to build:** A long scalar shows at most 200 characters. The person can expand that same cell to read the rest on the document's one scroll surface, then collapse it back to the short window. Opening a list does not open those full values by itself.

**Blocked by:** 01: Open a document as a record table; 05: Expand a list inside the cell

**Status:** resolved

- [x] A scalar cell shows at most 200 characters, taken from the start of the value, until the person expands it or a hit moves the window.
- [x] The value is not decoded into one string to paint the cell.
- [x] Expanding a truncated scalar grows that row to the virtual height of the text on the one scroll surface. Only the visible slice is decoded.
- [x] Collapsing it restores the 200-character window and the shorter row.
- [x] A scalar of 200 characters or fewer is shown in full and does not need the full-value expand control.
- [x] Expanding a list does not expand full values inside it.
- [x] Viewer session tests cover the window, a slice of a long value, collapse, and a list that stays collapsed to windows, and the window shows both states.
