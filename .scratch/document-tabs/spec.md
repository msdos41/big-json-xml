# Document tabs

Status: ready-for-agent

## Problem Statement

The window holds one Document. Open replaces it, clears Find what history, and drops the expanded cells, the scroll position, the column widths, the selection, and the hits the person was using. A second large document cannot stay open beside the first. Coming back means scanning again and finding the place again.

## Solution

The window keeps every opened Document until its tab is closed. A tab is one Viewer and that Viewer's Document. The active tab is the one shown on the document surface. The tab strip sits on one row under Open and Find and above that surface. From left to right a tab shows the file name, a filled circle for that tab's scan, and a large × that closes that tab.

Open appends a tab and makes it active, or, when the normalized full path is already open, activates that tab and leaves its place alone. On Windows that comparison ignores case. Switching away leaves the Viewer in place, including expanded cells, full values, both scroll positions, column widths, selection, scan progress, hits, the current hit, and the mark. A hidden tab's scan and search keep going. The status word, the scan fraction, the remaining time, and the error line follow the active tab. The circle is yellow while that tab is scanning, green when the scan is complete, and red when the scan has stopped.

Text search reads only the active tab's Document. There is no control for any other scope. Find what, the filters, and the history belong to the window. Hits belong to the tab that was searched. The Find window and the results window stay open across a switch and show the active tab's hits.

The strip has no horizontal scrollbar. When the tabs do not fit, the right end of the same row has a left arrow and a right arrow that move the row. The active tab is white with a blue bottom edge. The others are light gray. Nothing is restored after the process exits.

## User Stories

1. As a person inspecting documents, I want every Document I open to stay in the window, so that opening a second one does not discard the first.
2. As a person inspecting documents, I want one tab per open Document, so that I can see which documents the window is holding.
3. As a person inspecting documents, I want the tab strip on one row under Open and Find and above the document surface, so that Open, Find, and the table stay in their places.
4. As a person inspecting documents, I want the tabs in the order I opened them, so that a new document is at the end.
5. As a person inspecting documents, I want Open to append a tab and make it the active tab, so that the document I just chose is the one on the surface.
6. As a person inspecting documents, I want the file picker to choose one file each time, so that one Open adds one tab.
7. As a person inspecting documents, I want the tab's label to be the file name, so that the strip stays short.
8. As a person inspecting documents, I want two directories with the same file name to stay as two tabs with that same label, so that the label does not grow a directory prefix.
9. As a person inspecting documents, I want a file name longer than 160 device-independent pixels to end in an ellipsis, so that one name cannot push the others off the row by itself.
10. As a person inspecting documents, I want the circle and the × to stay on the tab when the file name ellipsizes, so that I can still read the scan and close the tab.
11. As a person inspecting documents, I want hovering the file name or the circle to show the full path, so that two identical file names can be told apart.
12. As a person inspecting documents, I want opening a path that is already open to activate that tab, so that I do not get a second Viewer of the same Document.
13. As a person inspecting documents, I want that match to use the normalized full path, so that `C:\a\.\b.xml` and `C:\a\b.xml` are one tab.
14. As a person inspecting documents, I want the match on Windows to ignore case, so that a different capitalization is still the same tab.
15. As a person inspecting documents, I want that activation to leave the scan, the expanded cells, and the scroll position as they were, so that Open is not a jump back to the start.
16. As a person inspecting documents, I want a missing path or a path I cannot read to add no tab, so that a failed Open does not disturb the documents already open.
17. As a person inspecting documents, I want that failure on the error line, so that I can see why the file did not open.
18. As a person inspecting documents, I want that failure to stay until I switch tabs or Open again, so that the next tick does not wipe it while I am still looking at the current tab.
19. As a person inspecting documents, I want a switch to replace the error line with the destination tab's own error, or to clear it when that tab has none, so that the line follows the active tab again.
20. As a person inspecting documents, I want the failed Open's message to stay gone after I return, so that a stale failure is not stuck on a healthy Document.
21. As a person inspecting documents, I want a failed Open with no tabs to leave the empty window and keep the message until a later Open, so that the failure is still visible when there is no tab to switch to.
22. As a person inspecting documents, I want a file that opens but whose scan is refused or stopped to become a tab, so that the stop has a Document to belong to.
23. As a person inspecting documents, I want that tab to become active, its circle red, and its stop on the error line, so that a bad Document is visible the same way a stop is today.
24. As a person inspecting documents, I want a stop that kept an outline to show that outline, so that the bytes accepted before the stop are not thrown away.
25. As a person inspecting documents, I want clicking the file name to make that tab active, so that I can change documents from the strip.
26. As a person inspecting documents, I want clicking the circle to make that tab active, so that the circle is part of the tab and not a second command.
27. As a person inspecting documents, I want a large × on each tab, larger than the circle, so that closing is an obvious control.
28. As a person inspecting documents, I want the × to close the tab it sits on, including a tab that is not active, so that I can drop a hidden Document without activating it first.
29. As a person inspecting documents, I want closing a hidden tab to leave the active tab active, so that the surface does not jump.
30. As a person inspecting documents, I want closing the active tab to activate the tab on its right, so that I land on a neighbor.
31. As a person inspecting documents, I want closing the rightmost tab to activate the tab on its left, so that a missing right neighbor still leaves a document on the surface.
32. As a person inspecting documents, I want the remaining tabs to keep their order, so that a close only removes the closed one.
33. As a person inspecting documents, I want close to ask nothing, so that a read-only Viewer does not pretend there is something to save.
34. As a person inspecting documents, I want the last tab's close to return the window to No document without exiting, so that I can Open something else.
35. As a person inspecting documents, I want no placeholder tab on that empty window, so that the strip is empty until I Open.
36. As a person inspecting documents, I want a closed tab to drop its outline, hits, and mark, so that a closed Document no longer spends memory.
37. As a person inspecting documents, I want closing the active tab to show the new active tab's hits in the results window, so that the list matches the surface.
38. As a person inspecting documents, I want closing a hidden tab to leave the results on screen unchanged, so that a background close does not rewrite the list I am reading.
39. As a person inspecting documents, I want expanded cells to still be expanded when I return to a tab, so that the embedded tables I opened are still open.
40. As a person inspecting documents, I want an expanded full value to still be expanded when I return, so that a scalar I opened stays open.
41. As a person inspecting documents, I want the horizontal and vertical scroll positions to be where I left them, so that I return to the same place on the one document surface.
42. As a person inspecting documents, I want a scroll position that no longer fits the viewport to clamp inside the document, so that resizing the window while I was on another tab does not send me back to the origin.
43. As a person inspecting documents, I want column widths to be as I left them, so that a width I dragged is still dragged.
44. As a person inspecting documents, I want the selected stretch in a cell to still be selected, so that copying after I return uses that selection.
45. As a person inspecting documents, I want the scan to continue from where it was, so that returning does not read the Document again from the first byte.
46. As a person inspecting documents, I want a hidden tab's scan to keep moving, so that a large Document makes progress while I read another.
47. As a person inspecting documents, I want a hidden tab's search to keep moving, so that Find All I started there finishes while I am elsewhere.
48. As a person inspecting documents, I want the status word, the scan fraction, and the remaining time to describe only the active tab, so that the toolbar does not mix two scans.
49. As a person inspecting documents, I want the existing determinate bar to stay on the toolbar for the active tab, so that the circle does not replace the scan fraction.
50. As a person inspecting documents, I want a hidden tab's circle to change as that scan moves, so that I can see yellow become green or red without activating it.
51. As a person inspecting documents, I want the circle yellow while that tab's scan is scanning, so that an unfinished read is visible on the tab.
52. As a person inspecting documents, I want the circle green when that tab's scan is complete, so that a finished read is distinct from one still running.
53. As a person inspecting documents, I want the circle red when that tab's scan has stopped, so that a refused grammar or a stopped scan is visible on the tab.
54. As a person inspecting documents, I want only those three circle colors, so that a stop is not given a fourth state.
55. As a person inspecting documents, I want an inactive tab's background to be `#E6E6E6` with black text, so that it recedes.
56. As a person inspecting documents, I want the active tab's background to be white with a `#1A56C4` bottom edge, so that the document on the surface is the one marked in the strip.
57. As a person inspecting documents, I want the circle colors to stay yellow, green, and red on both active and inactive tabs, so that the scan color is not confused with the tab color.
58. As a person inspecting documents, I want those circle colors to be `#E6B800`, `#2E8B57`, and `#C42B2B`, so that the three states match the light theme.
59. As a person inspecting documents, I want no horizontal scrollbar on the tab strip, so that the row has one way to move.
60. As a person inspecting documents, I want the mouse wheel to leave the tab strip where it is, so that the wheel is not a second strip scroller.
61. As a person inspecting documents, I want a left arrow and a right arrow at the right end of the row when the tabs do not fit, so that I can move the row without a scrollbar.
62. As a person inspecting documents, I want both arrows hidden, and the tabs aligned to the left, when every tab fits in the full row, so that arrows do not take space they do not need.
63. As a person inspecting documents, I want one click on an arrow to reveal the next tab clipped on that side, so that each click brings one whole tab into view.
64. As a person inspecting documents, I want the arrow on a side with nothing clipped to be disabled, so that the control shows that the row is already at that end.
65. As a person inspecting documents, I want the arrows to move the row without changing the active tab, so that looking further along the strip does not switch documents.
66. As a person inspecting documents, I want clicking a tab that is only partly in view to activate it and move the row until that tab is fully in view, so that its × is reachable after the click.
67. As a person inspecting documents, I want a newly opened tab that does not fit to be moved fully into view, so that the document I just opened is not left clipped.
68. As a person inspecting documents, I want activating an already open tab that is outside the visible row to move it fully into view, so that Open of a path I already have shows me that tab.
69. As a person inspecting documents, I want closing a tab to leave the row where it was, except to pull it back when empty strip would show past the last tab, so that a close does not jump to the first tab.
70. As a person inspecting documents, I want that pull-back to put the last tab's right edge on the right edge of the visible row, so that the gap is only the gap past the end.
71. As a person inspecting documents, I want the row to return to the left and hide the arrows when the remaining tabs fit, so that a shorter strip is left-aligned again.
72. As a person inspecting documents, I want the neighbor that becomes active to be moved fully into view when it is clipped, so that the document now on the surface has its whole tab visible.
73. As a person inspecting documents, I want Find, Find All, and Find Next to read only the active tab's Document, so that a hit belongs to the document I am looking at.
74. As a person inspecting documents, I want no scope control, so that search cannot be aimed at every open Document.
75. As a person inspecting documents, I want Find what, the name boxes, and the checkboxes to stay as I left them when I switch tabs, so that a sequence I typed is still there on the next Document.
76. As a person inspecting documents, I want Open to keep Find what history, so that a sequence I used on the previous Document is still in the list.
77. As a person inspecting documents, I want that history to stay newest first, at most 20 sequences, in memory for the life of the process, so that the existing history rule survives and is not written to disk.
78. As a person inspecting documents, I want the hits, the current hit, and the mark to stay on the tab I searched, so that switching away does not throw away a result list.
79. As a person inspecting documents, I want returning to show that tab's hits with the same current hit, and that hit visible in the results list, so that Find Next can continue where I stopped.
80. As a person inspecting documents, I want a tab I have not searched to show an empty results list, so that another tab's hits are not presented as this Document's.
81. As a person inspecting documents, I want a search with no hits to show that Document's group and a count of 0, so that I can see the search finished empty.
82. As a person inspecting documents, I want the Find window and the results window to stay open across a switch, in the place I put them, so that changing tabs does not dismiss or reshuffle them.
83. As a person inspecting documents, I want Find All to replace only the active tab's hits, so that a new search does not clear a hidden tab's list.
84. As a person inspecting documents, I want Find Next to walk the active tab's existing hits, so that it does not start a search on a tab that has none.
85. As a person inspecting documents, I want closing Find to clear the mark on the active tab only, and to close the results window, so that the surface I am looking at loses its mark and the other tabs keep theirs.
86. As a person inspecting documents, I want a hidden tab's mark to still be there when Find is closed, so that returning shows the cell I had marked.
87. As a person inspecting documents, I want opening Find again to leave a cleared mark cleared and to leave the results window closed, so that showing Find is not a new search.
88. As a person inspecting documents, I want Find All with no Document to do nothing, so that an empty window does not search.
89. As a person inspecting documents, I want Find to be allowed to stay open on that empty window, history included, so that closing the last tab does not discard the sequences I typed.
90. As a person inspecting documents, I want the Found column width to be one width for the window, so that switching tabs does not reset the column I dragged.
91. As a person inspecting documents, I want choosing a hit or Find Next to expand along that hit's path and scroll that Document's surface, so that reveal stays the one-document reveal.
92. As a person inspecting documents, I want Ctrl+F to do what the Find button does, so that the shortcut opens Find or brings the open Find window to the front.
93. As a person inspecting documents, I want Ctrl+F from the main window, the document surface, and the results window, so that the shortcut works from each of those places.
94. As a person inspecting documents, I want Ctrl+F to neither open a second Find window nor close Find, so that the shortcut cannot stack Finds or dismiss the one I have.
95. As a person inspecting documents, I want Esc to clear the active tab's mark and leave Find open, so that Esc still only clears the mark on the surface I am looking at.
96. As a person inspecting documents, I want Ctrl+C and Ctrl+A to apply to the active Document, so that copy and select-all follow the surface.
97. As a person inspecting documents, I want the window title to stay BigJsonXml, so that tabs do not rename the window.
98. As a person inspecting documents, I want a tab to keep its outline until I close it, so that expanding a cell after I return uses the outline the scan already kept.

## Implementation Decisions

- The desktop shell holds an ordered list of tabs and the index of the active tab. Each tab holds one Viewer for its Document. The shell does not add a second session type in the core. A Viewer is still one Document.
- Open normalizes the path to a full path with `.` and `..` removed, then compares ordinal-ignore-case on Windows. A match activates that tab and does not rebuild its Viewer. Any other path appends a tab, makes it active, and starts that Viewer's scan. A source opened with no path does not match another open by path; each such open appends a tab.
- The file is opened read-only the same way a single Document is opened today. The picker still accepts one file.
- An open that fails before a Viewer exists adds no tab and disposes nothing. The error line shows that failure until the active tab changes or Open is used again. A switch publishes the destination tab's error, which may be empty. Returning does not restore the failed-open message.
- A Viewer whose scan is refused or stopped still occupies a tab and becomes active. The circle follows that Viewer's scan progress: scanning, complete, or stopped. Stopped is red because a stop carries an error. There is no fourth progress color.
- Closing a tab disposes that Viewer and drops its hits and mark. The active tab is unchanged when the closed tab was not active. When it was active, the next tab in order becomes active, or the previous one when the closed tab was last. The last tab's close leaves the shell with no Viewer: status No document, no scan fraction, no remaining time, an empty surface, and an empty error line. The process stays up.
- Each tick pumps the active Viewer with the existing slice. It then gives each hidden Viewer that still has scan or search work one step in that same tick. Hidden work moves every tick, and the number of tabs does not multiply the slice into one full slice per tab. Only the active Viewer's frame, status, scan fraction, remaining time, and error are published. A failed-open message suppresses the active Viewer's error until the active tab changes or Open runs again.
- Find what, the name boxes, the checkboxes, the history, and the Found column width stay on the shell. History is still newest first and capped at 20, and Open does not clear it. Closing a tab does not clear it. Closing the last tab does not clear it.
- Hits, the current hit, and the mark stay on the Viewer that was searched. Find All and an empty Find what replace only the active Viewer's hits. Find Next walks that Viewer's hits and does not call a new search. The results window, while open, shows the active tab's hits: no group until that tab has been searched, then that Document's group and its count, including a count of 0. The current hit is kept in view in that list. Switching tabs does not move or close the Find window or the results window.
- Closing Find closes the results window and clears the active Viewer's mark only. Other Viewers keep their hits and marks. Opening Find again does not open the results window and does not recreate a cleared mark.
- Reveal, expand, scroll, column width, and selection remain the Viewer's existing behavior. The document surface still has one vertical scrollbar and one horizontal scrollbar. The shell applies the window's viewport to the active Viewer and keeps that Viewer's scroll, clamped to the scroll range.
- The strip is a view of the tab list. A tab's label is the file name. The circle is bound to that tab's scan progress and uses `#E6B800`, `#2E8B57`, and `#C42B2B`. An inactive tab uses `#E6E6E6` and black text. The active tab uses white and a `#1A56C4` bottom edge. The × closes that tab and is larger than the circle. The file-name run ellipsizes at 160 device-independent pixels. Hovering the name or the circle shows the normalized full path.
- The strip measures fit against the full row. When every tab fits, the arrows are hidden and the offset is the start. When they do not, the arrows occupy the right end and the tabs clip in the width that remains. One click changes the offset so the next clipped tab on that side is fully visible. An arrow with nothing clipped on its side is disabled. A click does not repeat while held. The arrows do not change the active tab. Activating a tab, by click, by Open, or by the neighbor rule, then adjusts the offset until that tab is fully visible. After a close, the offset is unchanged and then clamped so the visible row does not show empty strip past the last tab; if the remainder fits, the offset returns to the start.
- The mouse wheel does not change the strip offset. Ctrl+F invokes the same command as the Find button from the main window, the document surface, and the results window. The Find window does not spawn another Find.
- Esc, Ctrl+C, and Ctrl+A keep their present meaning on the active Document. The window title stays BigJsonXml.
- Tabs live in the process only. Closing the window disposes every remaining Viewer.

## Testing Decisions

- Test what a person can observe. A good test opens documents, switches and closes tabs, and asserts the active surface, the scroll, the expanded cells, the selection, the status, the error line, the find fields, the history, and the hits. It does not read a private expand set or a private hit buffer.
- One seam: the desktop shell, where the existing shell tests already drive the window model and the main window. Tab behavior is asserted on that model. The file name, circle, ×, colors, arrows, truncation, and Ctrl+F are asserted on the main window, in the same way those tests already drive Find and the results window.
- Do not add a core seam. A Viewer remains one Document, and a session test does not learn about tabs.
- Prior art is the shell coverage for opening a Document, expanding a cell, running Find, revealing a hit, and showing the main window. Expectations that Open replaces the only Document, or that Open clears history, change with this spec in the shell tests that cover them.
- A hidden tab's progress is observed from the tab list the strip shows, including a circle that changes while another tab is active. Preservation is observed by leaving and coming back, not by a test-only snapshot.

## Out of Scope

- Search across more than the active Document, and any scope control.
- Opening by drag-and-drop, or picking more than one file.
- Reordering tabs, a context menu, middle-click close, Ctrl+W, and Ctrl+Tab.
- Restoring tabs after the process exits.
- Noticing that a file changed on disk and scanning it again.
- A cap on how many tabs may be open.
- A dark theme. The colors above are for the light theme.
- Holding an arrow so that it repeats.
- A second Find window, replace, or any edit. The product stays a Viewer.

## Further Notes

- ADR-0046 records one resident Viewer per open Document, active-tab search, and no restore after exit. ADR-0047 records window-level find criteria and tab-level hits. ADR-0013 still records reveal. ADR-0015 still records the one document scrollbar pair. ADR-0001 still records that there is nothing to save.
- The find spec's history was per open Document and was cleared when that Document went away. This spec replaces that with the window history in ADR-0047.
- The find spec allowed a later Document to sit as another results group while search still read one Document. This spec shows the active tab's group only. Groups are not stacked for every open tab.
- The agreed test seam is the desktop shell alone.
