# 03: Keep list entry places after collapse

**What to build:** After the scan has walked a list, the person can collapse it and later open an entry near the end without the viewer reading the other entries again. The places were learned while those siblings were scanned. Putting the list away does not throw them away.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Collapsing a long list and then reading an entry near the end does not take time or allocation proportional to reading the whole list again.
- [x] Those places come from the scan that was already walking the siblings, not from a second pass after Scanning ends.
- [x] Expanding the same list again shows the same entries and fields.
- [x] While an expanded list is still being scanned, the scroll range grows as further entries are found, and an entry already seen can be opened.
- [x] A search hit is still a path. Search does not read the list's entry places.
- [x] This replaces the older acceptance that collapsing a list may drop its offsets. A Viewer session scale test uses a generated list large enough that reading every earlier entry would dominate. No multi-GB fixture is added.
