# 01: Keep each opened Document

**What to build:** The window keeps every Document a person opens, on a tab strip under Open and Find and above the document surface. Open appends a tab and shows that Document. Opening the same path again only activates the tab already holding it. Clicking a file name switches documents. Coming back shows the same expanded cells, full values, scroll position, column widths, selection, and outline. A large × closes the tab it sits on. Closing the last tab leaves No document and does not exit. A path that cannot be opened adds no tab and explains itself on the error line.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] The strip is one row under Open and Find and above the document surface. Tabs appear in open order. A new Open appends a tab and makes it active, and the surface shows that Document.
- [ ] The file picker still chooses one file. The label is the file name, so two directories with the same file name are two tabs with the same label. A file name longer than 160 device-independent pixels ends in an ellipsis, and the × stays on the tab. Hovering the file name shows the normalized full path.
- [ ] The same normalized full path activates the existing tab. On Windows the comparison ignores case, and `C:\a\.\b.xml` is the same tab as `C:\a\b.xml`. Activation does not scan again and does not move expanded cells or scroll. A source with no path does not match another open by path; each such open appends a tab.
- [ ] Clicking the file name makes that tab active and shows its Document. Expanded cells, expanded full values, both scroll positions, column widths, the selected stretch, and the outline are as they were left. A scroll position that no longer fits the viewport clamps inside the document instead of jumping to the origin.
- [ ] The status word, the scan fraction, and the remaining time describe only the active tab. The determinate bar stays on the toolbar for that tab. The window title stays BigJsonXml. The document surface still has one vertical scrollbar and one horizontal scrollbar.
- [ ] An inactive tab uses `#E6E6E6` and black text. The active tab is white with a `#1A56C4` bottom edge.
- [ ] The × is larger than the scan circle's place and closes the tab it sits on, including a tab that is not active. Closing a hidden tab leaves the active tab active. Closing the active tab activates the tab on its right, or the tab on its left when the closed tab was last. Remaining tabs keep their order. Close asks nothing.
- [ ] Closing the last tab returns the window to No document without exiting: no placeholder tab, no scan fraction, no remaining time, an empty surface, and an empty error line. A closed tab drops its outline.
- [ ] A missing path or a path that cannot be read adds no tab and disposes nothing. The error line shows that failure until the person switches tabs or Opens again. A switch then shows the destination tab's own error, or clears the line when that tab has none. Returning does not bring the failed-open message back. With no tabs, the empty window keeps the message until a later Open.
- [ ] A file that opens but whose scan is refused or stopped still becomes the active tab, and any outline kept before the stop is shown.
- [ ] Esc, Ctrl+C, and Ctrl+A keep their present meaning on the active Document. Tests talk only to the desktop shell. They observe the strip and the surface by switching away and back, and they do not read a private expand set.
