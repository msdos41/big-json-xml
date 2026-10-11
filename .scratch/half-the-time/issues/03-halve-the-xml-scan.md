# 03: Halve the XML scan

**What to build:** A person inspecting an XML document waits at most half as long for the scan as the baseline in ticket 01. The same reading serves a repeated list, a nested list, a deep record, a tree of distinct names, and a harness document. It does not key off element names or a fixed depth. A `.vxf` document is this same XML document. When the scan ends, the outline is the one pinned in ticket 02.

**Blocked by:** 01: Record the signing baseline. 02: Pin the outline, the hits, and the column fit.

**Status:** ready-for-agent

- [ ] Every XML document in the timing set, including each `.vxf` document, finishes scanning in at most half the median recorded for it in ticket 01.
- [ ] A document whose baseline scan is already within twice a sequential read of that file is recorded as missing the half and is no slower than that baseline.
- [ ] The outline matches ticket 02. Entry places survive a collapse. A closed record keeps an outline and no second body.
- [ ] One slice of the scan still returns well under a tenth of a second. The scan runs to the end of the document and is not cut off to make the half.
- [ ] A local document of about 500MB still finishes within fifteen seconds, both as repeated siblings and as distinct names. That bar is not tightened.
- [ ] The working set after the scan, before a large hit list, is at most one and a half times the figure recorded for that document in ticket 01.
- [ ] An external entity still stops the scan. An external subset stays unread. Whitespace-only gaps are still not fields. The grammar is still chosen from the first byte, not the file name.
- [ ] A generated document of a different shape is no slower to scan than it is today, and a text search of it returns the hits pinned in ticket 02.
