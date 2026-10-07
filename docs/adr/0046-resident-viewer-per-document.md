# A window holds one resident viewer per open document

Open keeps each document until its tab is closed. The same full path activates the tab that already holds it, and on Windows that comparison ignores case, instead of scanning a second copy. Switching tabs leaves each viewer in place, including expanded cells, full values, both scroll positions, column widths, selection, and a scan still in progress. The surface, the status line, and text search follow the active tab. Search has no all-documents mode. Tabs last only as long as the window.

Replacing the open document was rejected because the opened documents would not stay. Disposing a viewer on switch was rejected because coming back would scan again and lose the place. Searching every open document was rejected because a hit is revealed in one document, and this pass adds no scope control. Restoring tabs after the process exits was rejected.
