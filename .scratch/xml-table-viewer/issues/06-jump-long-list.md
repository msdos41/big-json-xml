# 06: Jump within a long list

**What to build:** On an expanded list that is still being scanned, the person can scroll to an entry near the end without the viewer building an object for every earlier entry. Closing the list releases that cost. Opening it again still shows the right entries.

**Blocked by:** 05: Expand a list inside the cell

**Status:** resolved

- [ ] While an expanded list is still being scanned, the scroll range grows as further entries are found, and the first rows stay readable.
- [ ] Reading a slice at an entry near the end does not require an object per earlier entry.
- [ ] Collapsing the list allows that per-list offset information to be dropped.
- [ ] Expanding the same list again shows the same entries and fields.
- [ ] A Viewer session scale test uses a generated list large enough that an object-per-entry approach would dominate, and reads a slice at the end. No multi-GB fixture is added.
- [ ] The window can scroll that expanded list to a late entry on the one scroll surface.
