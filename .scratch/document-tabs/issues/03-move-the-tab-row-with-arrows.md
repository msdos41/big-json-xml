# 03: Move the tab row with arrows

**What to build:** When the tabs do not fit on the one row, a left arrow and a right arrow at the right end move the row. There is no horizontal scrollbar. One click brings the next clipped tab on that side fully into view. The arrows do not change which Document is active. A tab the person opens, activates, or lands on after a close is moved fully into view when it is clipped.

**Blocked by:** 01: Keep each opened Document

**Status:** resolved

- [x] The strip has no horizontal scrollbar. The mouse wheel does not move the row.
- [x] Fit is measured against the full row. When every tab fits, both arrows are hidden and the tabs align to the left. When they do not, the arrows occupy the right end and the tabs clip in the width that remains.
- [x] One click reveals the next tab clipped on that side, and holding the button does not repeat. An arrow with nothing clipped on its side is disabled and does not move the row. The arrows do not change the active tab.
- [x] Clicking a tab that is only partly in view activates it and moves the row until that tab is fully in view.
- [x] A newly opened tab that does not fit is moved fully into view. Activating an already open tab that sits outside the visible row does the same.
- [x] Closing a tab leaves the row where it was, then pulls it back only far enough that empty strip does not show past the last tab. That pull-back puts the last tab's right edge on the right edge of the visible row. When the remaining tabs fit, the row returns to the left and the arrows hide.
- [x] When closing the active tab makes a neighbor active, that neighbor is moved fully into view if it is clipped.
- [x] Tests talk only to the desktop shell and judge the row by which tabs are fully in view.
