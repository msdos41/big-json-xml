# Half the time on the general walk

Scan, text search, expand, scroll, column drag, and column fit each finish in at most half the wall-clock time of the current viewer. The saving is less temporary work on the forward readings the viewer already does. JSON and XML may use different code. Neither walk keys off a sample's names, depth, or schema. `.vxf` is XML by its first byte.

The clock is an in-process stopwatch around that operation: not process start, not the file dialog, and not painting. A slower paint is a regression and is not part of the half. Release builds, three successive runs in one process, the median. The viewport is 800 by 600. Every file in `C:\D\OPC\data` is in the gate.

A resident search index stays rejected, and text search still does not read the list-entry offsets (ADR 0024, ADR 0044). Extra threads stay out until this single-threaded walk has nothing left to give up. ADR 0044 set no time ratio and kept the working set in the band already measured. This decision adds the half, and it allows the peak working set to grow by half: after the scan and before a large hit list, at the end of the signing search, and during the signing column fit. Measured text is not kept. Expand, scroll, and drag do not keep a second surface.

A scan whose median is already within twice a sequential read of that file, on the same machine, is recorded as missing the half and must not get slower. Text search uses the same exit against that same read. Scan and column fit still yield about every 32 milliseconds.

The signing search is the default text search: case-sensitive contains, names and values included, no field or parent limit. Find what is three characters, fixed per file, with at least one hit and no more than two thousand. Other search conditions stay correct and must not get slower.

After the scan, the gestures use the largest list, the first such list when counts tie. Expand it once. Scroll one viewport down, and one viewport across; a remainder shorter than a viewport moves to the end of that axis. An axis that does not overflow must not get slower, and is not required to halve. Drag the first data column ten character widths wider, then column-fit that same column. The expand is not included in the fit. A document with no list uses the open record's value column for the drag and the column fit, one scroll each way, and the first expandable cell for the expand. Column fit still reads every entry and still stops at five hundred characters, so the width stays the width it already produces. Other columns must not get slower.

Existing tests remain the behavior.
