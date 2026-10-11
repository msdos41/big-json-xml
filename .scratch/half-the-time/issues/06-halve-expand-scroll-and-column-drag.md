# 06: Halve expand, scroll, and column drag

**What to build:** A person inspecting a document waits at most half as long to expand the largest list, to scroll one viewport down, to scroll one viewport across, and to drag the first data column ten character widths wider. The viewport is 800 by 600. Only the visible entries are realized. The document does not gain a second surface. An axis that already fits is not required to halve.

**Blocked by:** 03: Halve the XML scan. 04: Halve the JSON scan.

**Status:** ready-for-agent

- [ ] On every timing document that has a list, the first expand of the largest list finishes in at most half the median recorded in ticket 01. The visible entries appear. The rest of the document is not walked.
- [ ] A vertical scroll of one viewport, and a horizontal scroll of one viewport, each finish in at most half the median recorded in ticket 01 when that axis overflows. A remainder shorter than a viewport moves to the end of that axis.
- [ ] An axis that does not overflow is no slower than its baseline and is not required to halve.
- [ ] Dragging the first data column ten character widths wider finishes in at most half the median recorded in ticket 01. The floors on a drag stay where they are. A shared right edge still moves the columns it moves today.
- [ ] Every other column drag is no slower than it is today.
- [ ] A document with no list expands its first expandable cell, scrolls both axes, and drags its value column, under the same rules.
- [ ] The clocks stop when the surface data is ready. Painting is not in the clock. A slower paint is a regression.
- [ ] Expand, scroll, and drag do not keep a second copy of the surface. Two lists of equal count still use the earlier one.
