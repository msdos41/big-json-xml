# Find criteria belong to the window, hits belong to the tab

The Find window keeps one Find what, one set of filters, and one history. Opening another document does not clear that history. Hits, the current hit, and the mark stay on the tab that was searched. Switching tabs leaves the Find and Results windows open and shows the active tab's hits. A search still running on a hidden tab keeps going. Closing Find clears the mark on the active tab only.

Per-tab criteria were rejected because the text just typed would disappear on switch. Dropping hits on switch was rejected because Find Next would lose its place. Clearing history on Open belonged to the single-document window and was rejected once Open keeps the previous document.
