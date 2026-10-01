# 07: Find what history

**What to build:** The Find what box remembers the sequences used while this document is open, so a person can run one again. The list is newest first, holds at most 20, and disappears when the document closes.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** ready-for-agent

- [ ] Each Find what that is searched is added to the dropdown, newest first.
- [ ] The dropdown holds at most 20 sequences.
- [ ] Choosing an older sequence puts it in Find what.
- [ ] Closing the document drops the list. Nothing is written to disk.
- [ ] Opening another document does not show the previous document's sequences.
- [ ] Window-model tests cover the order, the cap of 20, and closing the document.
