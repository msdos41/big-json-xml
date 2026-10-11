# 05: Halve the default text search

**What to build:** A person inspecting a document waits at most half as long for the default text search as the baseline in ticket 01. The search is still a second forward reading of the whole document. It does not read list-entry offsets and it does not leave an index. JSON and XML keep the readings made faster in tickets 03 and 04. The hits are the hits pinned in ticket 02.

**Blocked by:** 03: Halve the XML scan. 04: Halve the JSON scan.

**Status:** resolved

- [x] Every document in the timing set finishes the signing search in at most half the median recorded for it in ticket 01. Find what is the same three-character sequence used for that baseline, with at least one hit and no more than two thousand.
- [x] A search whose baseline is already within twice a sequential read of that file is recorded as missing the half and is no slower than that baseline.
- [x] Each hit keeps its path, match offset, and match length, in document order. Two matches in one field stay two hits.
- [x] Starts with, entire cell, Match case off, and a field name, parent element name, grandfather element name, or parent attribute name keep the hits pinned in ticket 02 and are no slower than they are today.
- [x] An empty Find what has no hits. Comments are not hits. A JSON attribute name and a JSON parent attribute match nothing. A JSON value stays the literal the cell shows.
- [x] The working set at the end of the signing search is at most one and a half times the figure recorded for that document in ticket 01.
- [x] Hits can still appear while a scan is in progress. The search does not consult list-entry offsets.
