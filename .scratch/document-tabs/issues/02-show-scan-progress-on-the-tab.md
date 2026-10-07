# 02: Show each scan on its circle

**What to build:** Each tab shows a filled circle between the file name and the ×. The circle is yellow while that Document is scanning, green when the scan is complete, and red when the scan has stopped. A hidden tab's circle changes as that scan moves, and a hidden scan or search keeps making progress. The toolbar still describes only the active tab.

**Blocked by:** 01: Keep each opened Document

**Status:** ready-for-agent

- [ ] The circle sits between the file name and the ×. Clicking it activates the tab and does not close it. Hovering it shows the same full path as the file name.
- [ ] Scanning uses `#E6B800`, complete uses `#2E8B57`, and stopped uses `#C42B2B`. These three colors are the only ones, on both the active tab and inactive tabs. A refused grammar or any other stop is red.
- [ ] When the file name ellipsizes, the circle and the × both stay on the tab.
- [ ] A hidden tab's circle changes while another tab is active, including from yellow to green or red.
- [ ] Each tick still gives the active Viewer the existing slice. Each hidden Viewer that still has scan or search work then takes one step in that same tick. Hidden work moves every tick, and more tabs do not turn the slice into one full slice per tab.
- [ ] The status word, the scan fraction, the remaining time, and the toolbar bar still follow only the active tab.
- [ ] Tests talk only to the desktop shell. A hidden tab's progress is read from the strip, not from a private cursor.
