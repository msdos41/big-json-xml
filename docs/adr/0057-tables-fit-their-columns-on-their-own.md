# Tables fit their columns on their own

Every list column started at 200 px until the user double-clicked its edge. A table now fits its columns once, on its own, when it is first drawn: after a file is opened and after an expand.

1. The fit waits for the scan to finish. It runs on the viewer's timer in slices of at most 8 ms, after the table has been drawn once, so it never holds up opening, expanding, or scrolling.
2. It reads a small sample of the table, with all columns read together: about 16,384 cells, but at least 16 and at most 512 entries. The entries are taken in runs of 32 neighbours spread over the list, the first run at the top. The cost does not grow with the number of rows.
3. The width of each column follows the rule of ADR 0056: the header, or the sampled cells without their longest twentieth, capped at 60 characters.
4. A column the user has dragged or double-clicked is never fitted on its own again. Each table is fitted once. Scrolling to rows with longer values does not widen it; a double-click still fits from the larger sample of ADR 0056.
5. A table with nothing open inside it takes all its widths in one step. A table with an open cell takes them one column at a time, as a drag would.

The session leaves this off (`ViewerSession.AutoFitColumns`), so code that reads a session keeps the 200 px default. The viewer turns it on (`ViewerModel.AutoFitColumns`).
