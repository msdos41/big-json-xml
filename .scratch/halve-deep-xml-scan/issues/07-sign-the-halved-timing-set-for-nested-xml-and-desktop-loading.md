# 07: Sign the halved timing set for nested XML and desktop loading

**What to build:** A person inspecting any document in the timing set, including deeply nested multi-list documents, can see on the spec that the signed scan clock is at most half the baseline from ticket 01. The outline, list counts, entry places, and column fit widths are identical to those pinned in ticket 02. The window still takes user input during long scans. The peak working set remains at most one and a half times the baseline figure recorded in ticket 01. The new timings are appended to the spec.

**Blocked by:** 06: Halve desktop interface wait with adaptive scan slicing and throttled refresh.

**Status:** ready-for-agent

- [ ] Every document in the target benchmark set, including `4d7480cde29a662758904cbe0e1afdbb-BD_.xml`, is measured through the viewer session under the protocol from ticket 01 (Release, median of three runs, 800x600 viewport).
- [ ] The scan clock on `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` finishes in at most half the baseline recorded in ticket 01 (under 3.1 seconds in Core session scan).
- [ ] The desktop viewer scan completion wall-clock time on `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` finishes in at most half the baseline recorded in ticket 01 (under 5.5 seconds).
- [ ] No existing document in the timing set is slower than its baseline recorded in ticket 01.
- [ ] The working set after the scan is at most one and a half times the figure recorded in ticket 01.
- [ ] The new timing table is appended to the spec.
