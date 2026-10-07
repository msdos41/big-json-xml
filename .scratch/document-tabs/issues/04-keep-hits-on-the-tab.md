# 04: Keep hits on the tab

**What to build:** Text search reads only the Document on the active tab. Find what, the filters, and the history stay with the window. The hits, the current hit, and the mark stay with the tab that was searched. Switching tabs leaves the Find window and the results window open and shows the active tab's hits. Ctrl+F does what the Find button does.

**Blocked by:** 01: Keep each opened Document

**Status:** resolved

- [x] Find, Find All, and Find Next read only the active tab's Document. There is no scope control.
- [x] Find what, the name boxes, and the checkboxes stay as left when the person switches tabs. Open does not clear history. History stays newest first, at most 20 sequences, in memory for the life of the process, including after the last tab closes.
- [x] Hits, the current hit, and the mark stay on the tab that was searched. Returning shows that list, with the same current hit visible in the results window. Find All replaces only the active tab's hits.
- [x] A tab that has not been searched shows an empty results list. A search with no hits shows that Document's group and a count of 0.
- [x] The Find window and the results window stay open across a switch, in the place the person put them. The Found column width is one width for the window and is not reset by a switch.
- [x] Find Next walks the active tab's existing hits and does not start a search on a tab that has none. Choosing a hit or Find Next still expands along that hit's path and scrolls that Document's surface.
- [x] Closing Find clears the mark on the active tab only and closes the results window. A hidden tab's mark is still there when Find is closed. Opening Find again does not restore a cleared mark and does not open the results window.
- [x] Find All with no Document does nothing. Find may stay open on the empty window, history included.
- [x] Ctrl+F from the main window, the document surface, and the results window opens Find or brings the open Find window to the front. It does not open a second Find window and does not close Find.
- [x] Tests talk only to the desktop shell. They observe hits by what the results window and the active surface show after a switch.
