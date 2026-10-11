# 07: Halve column fit

**What to build:** A person inspecting a document waits at most half as long for a column fit of the first data column as the baseline in ticket 01. The list is already expanded, so the expand is not part of this wait. The fit still reads every entry and still stops at five hundred characters, and the width it applies is the width it applies today. On a list of short cells the wait may still be seconds after the half. The window keeps accepting input throughout.

**Blocked by:** 03: Halve the XML scan. 04: Halve the JSON scan.

**Status:** ready-for-agent

- [ ] On every timing document that has a list, the column fit of the first data column finishes in at most half the median recorded in ticket 01.
- [ ] The applied width matches the width pinned in ticket 02 for a long list of short cells, and it matches the width the current viewer produces on that timing document. Entries are not sampled.
- [ ] The fit stops growing once a cell is already five hundred characters. It does not stop at a fixed count of entries.
- [ ] One slice of the fit still returns well under a tenth of a second.
- [ ] The working set during the fit is at most one and a half times the figure recorded for that document in ticket 01. The text measured for the fit is not kept.
- [ ] A document with no list column-fits its value column under the same rules. Every other column fit is no slower than it is today.
- [ ] The clock stops when the width is applied. Painting is not in the clock. There is no separate bar that the fit finish within one second.
