# 01: Record the signing baseline

**What to build:** Nothing gets faster yet. A person inspecting a document can read, on the spec, how long the current viewer takes for every document in the timing set. The clocks are the scan, the default text search, the first expand of the largest list, a vertical scroll of one viewport, a horizontal scroll of one viewport, a drag that widens the first data column by ten character widths, and a column fit of that same column. The viewport is 800 by 600. Each clock is the median of three successive runs in one process, on a Release build. The table also records a sequential read of each file, and the working set after the scan, at the end of the signing search, and during the signing column fit.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Every file in the timing set is measured through the viewer session. The documents stay outside the repository.
- [x] The scan clock runs from the start of the forward reading to the completed outline. Process start and the file dialog are not included.
- [x] The signing search is the default text search. Find what is three characters, fixed per document, with at least one hit and no more than two thousand. Its clock ends when the hit list is complete.
- [x] After the scan, the gestures use the list with the greatest entry count, or the earliest when counts tie. Expand once, then scroll down one viewport, then scroll across one viewport, then drag the first data column ten character widths wider, then column-fit that column. A remainder shorter than a viewport moves to the end of that axis.
- [x] The first data column is the first column that is not the entry index. The expand is not included in the column-fit clock. The list is already expanded when the fit starts.
- [x] A document with no list uses the open record's value column for the drag and the column fit, one scroll on each axis, and the first expandable cell for the expand.
- [x] An axis whose content already fits is recorded as not required to halve.
- [x] The three working-set points and the sequential-read time are recorded beside the clocks.
- [x] The table is appended to the spec. The numbers already in the spec's further notes are not reused as this baseline. The viewer does not change, and the suite does not gain a seconds target.
