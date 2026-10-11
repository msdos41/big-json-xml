# 04: Halve the JSON scan

**What to build:** A person inspecting a JSON document waits at most half as long for the scan as the baseline in ticket 01. The reading may differ from the XML reading. It still serves an object, an array of objects, a root array, a nested array, and a scalar, at any depth, and it does not key off the names in the timing set. When the scan ends, the outline is the one pinned in ticket 02.

**Blocked by:** 01: Record the signing baseline. 02: Pin the outline, the hits, and the column fit.

**Status:** ready-for-agent

- [ ] Every JSON document in the timing set finishes scanning in at most half the median recorded for it in ticket 01.
- [ ] A document whose baseline scan is already within twice a sequential read of that file is recorded as missing the half and is no slower than that baseline.
- [ ] The outline matches ticket 02. A JSON array of any length is a list. A repeated key still stops the scan. Empty text, whitespace only, or a second value still does not open.
- [ ] One slice of the scan still returns well under a tenth of a second. The scan runs to the end of the value and is not cut off to make the half.
- [ ] The working set after the scan, before a large hit list, is at most one and a half times the figure recorded for that document in ticket 01.
- [ ] A huge scalar stays a window over its place in the document. The grammar is still chosen from the first byte, not the file name.
- [ ] A generated JSON document of a different shape is no slower to scan than it is today, and a text search of it returns the hits pinned in ticket 02.
