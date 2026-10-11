# 08: Sign the timing set

**What to build:** A person inspecting any document in the timing set can see, on the spec, that each signed clock is at most half the baseline from ticket 01, or is named as excused and no slower. The excuses are a scan or a signing search already within twice a sequential read, and a scroll axis that does not overflow. The outline, the hits, and the fitted widths are unchanged. The window still accepts input during a scan and a column fit.

**Blocked by:** 05: Halve the default text search. 06: Halve expand, scroll, and column drag. 07: Halve column fit.

**Status:** ready-for-agent

- [ ] Every file in the timing set is measured again through the viewer session, with the protocol and the Find what strings from ticket 01. The documents stay outside the repository.
- [ ] Each scan, signing search, expand, overflowing scroll, column drag, and column fit is at most half its baseline median. An excused clock is no slower than its baseline, and the excuse is written down.
- [ ] The working set after the scan, at the end of the signing search, and during the column fit is at most one and a half times the matching figure in ticket 01.
- [ ] The new times are appended to the spec. The suite does not encode them as a seconds target. The fifteen-second scan floor still holds and is not tightened.
- [ ] The behavior pinned in ticket 02 still passes, including every text-search condition, entry places, and the fitted width of a long list of short cells.
- [ ] A slice of scan work and a slice of column fit still return well under a tenth of a second.
