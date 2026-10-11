# Half the time on the general walk

Status: ready-for-agent

## Problem Statement

A person opens a document from the local timing set and waits twice: once while the viewer is scanning, and again for a text search that reads the document forward a second time. On the files already measured, that wait is the walk, not the disk. A 40MB XML document takes longer to scan than a 205MB JSON document. A text search with only a couple of hits costs about as much as another scan.

Moving around the document has its own wait. Expanding, scrolling, and dragging a column are quick when only the visible entries are drawn. A column fit is not. On a list of hundreds of thousands of short cells, the fit reads every entry, because none of them reaches five hundred characters and stops it. The window yields during that work, but the wait is the sum of those yields, and it is long enough to notice.

The person wants every one of those waits cut in half, on every file in the timing set, including the small ones. The outline, the hits, and the fitted width have to stay what they are now. The window still has to take input while a long scan or a long column fit is running.

## Solution

Scan, the default text search, expand, scroll, column drag, and column fit each finish in at most half the wall-clock time they take on the current viewer. The clock starts at the operation and stops when the surface data is ready. It does not include starting the process, choosing the file, or painting pixels.

The half comes from less temporary work on the forward readings the viewer already does. JSON and XML stay separate grammars and may be made faster by different code. Neither reading keys off a sample's names, depth, or schema. A `.vxf` document is an XML document because its first byte says so.

A resident search index is not added. Text search does not start reading list-entry offsets. Extra threads are not added. Column fit still measures every entry and still stops at five hundred characters, so the width it applies is the width it applies today. Peak working set may grow by at most half, and only against the same operation on the current viewer.

## User Stories

1. As a person inspecting a document, I want every document in the timing set to finish scanning in at most half the time it takes today, so that a large document and a small one are both held to the same ratio.
2. As a person inspecting a document, I want a document of a few megabytes to be held to that same half, so that a short scan is not excused for being short.
3. As a person inspecting a document, I want a JSON document and an XML document to each take at most half the time they take today, so that one grammar is not left behind.
4. As a person inspecting a document, I want a `.vxf` document to be scanned as the XML document it already is, so that the file name does not choose a third grammar.
5. As a person inspecting a document, I want the first byte after the leading mark and whitespace to keep choosing the grammar, so that a renamed file still opens the way it does today.
6. As a person inspecting a document, I want a repeated list, a tree of distinct field names, a nested list, and a deep record to use the reading they already use, so that the half is not reserved for one sample.
7. As a person inspecting a document, I want a harness document and a root JSON array to use that same reading, so that a shape which is not the employee list still gets the half.
8. As a person inspecting a document, I want the reading to ignore the names in the timing set, so that a later document with different names is not left on the slow path.
9. As a person inspecting a document, I want UTF-8 text, including non-ASCII characters, to stay on that reading, so that a character the grammar already accepts does not force a heavier path.
10. As a person inspecting a document, I want the scan to run to the end of the document, so that the half is not bought by stopping early.
11. As a person inspecting a document, I want the outline at the end of the scan to be the outline I get today, so that names, kinds, list counts, and columns do not change because the reading got faster.
12. As a person inspecting a document, I want each list entry's place to stay after the scan, so that I can still open any entry without reading the others.
13. As a person inspecting a document, I want collapsing a list or a record to keep what the scan stored, so that the next open does not walk the document again.
14. As a person inspecting a document, I want a closed record to keep its outline and no second body, so that a faster scan does not start retaining a parsed copy per element.
15. As a person inspecting a document, I want the window to keep accepting input during the scan, so that a faster reading does not freeze the viewer until the end.
16. As a person inspecting a document, I want each pause in the scan to stay about a single slice, so that scrolling and expanding are still handled many times a second.
17. As a person inspecting a document, I want the scan fraction and the remaining time to keep updating, so that a faster reading does not hide how far the scan has reached.
18. As a person inspecting a document, I want a document larger than the ones already timed to take longer because it is larger, and still at most half of its own time today, so that the reading is not abandoned at a fixed size.
19. As a person inspecting a document, I want a scan that is already within twice a sequential read of that file to be recorded as missing the half and to get no slower, so that the viewer does not skip bytes or add an index to beat the disk.
20. As a person inspecting a document, I want a scan that stops on an ill-formed byte to stop where it stops today, so that the outline built before the stop is the same outline.
21. As a person inspecting a document, I want an external entity to stop the scan as it does today, so that a faster reading does not fetch outside the document.
22. As a person inspecting a document, I want an external subset named by a document type declaration to stay unread, so that a harness document does not start fetching a declaration it does not fetch today.
23. As a person inspecting a document, I want an internal entity to expand as it does today, so that a faster reading does not skip or double that text.
24. As a person inspecting a document, I want indentation made of spaces, tabs, carriage returns, and line feeds to stay out of the outline, so that a pretty-printed document does not gain blank fields.
25. As a person inspecting a document, I want comments and processing instructions to stay absent, so that speed does not surface them as fields.
26. As a person inspecting a document, I want a namespace to keep its displayed prefix and its identity, so that a faster reading does not flatten qualified names.
27. As a person inspecting a document, I want attribute names written with a leading `@`, so that the table language is unchanged.
28. As a person inspecting a document, I want a bare attribute to stay an empty value, so that one flag does not stop the rest of the document.
29. As a person inspecting a document, I want a name seen twice to become a list and stay a list, so that promotion still happens once.
30. As a person inspecting a document, I want a JSON document that is empty, whitespace only, or two values to be refused as it is today, so that a faster reading does not start accepting those files.
31. As a person inspecting a document, I want a repeated JSON key to stop the scan as it does today, so that a faster reading does not invent a second field.
32. As a person inspecting a document, I want a huge scalar to stay a window over its place in the document, so that a faster scan does not decode it into one string.
33. As a person inspecting a document, I want the existing time bar for a document of about 500MB to keep holding, so that this work does not make that scan late.
34. As a person inspecting a document, I want the default text search of every timing document to finish in at most half the time it takes today, so that the second reading is held to the same ratio as the scan.
35. As a person inspecting a document, I want that search to be the default one, so that case-sensitive contains, with names and values included and no field or parent limit, is the search that has to halve.
36. As a person inspecting a document, I want Find what for that search to be three characters, fixed for that document, so that the before and after times are the same search.
37. As a person inspecting a document, I want that Find what to produce at least one hit and no more than two thousand, so that the half measures the reading and not a huge list of hits.
38. As a person inspecting a document, I want those hits to keep their path, the stretch that matched, and their document order, so that Find next and reveal land on the same cells.
39. As a person inspecting a document, I want two matches in one field to stay two hits, so that a faster comparison does not collapse them.
40. As a person inspecting a document, I want starts-with, entire-cell, Match case off, and a field, parent, grandfather, or parent-attribute limit to keep today's hits and to get no slower, so that only the default search has to halve.
41. As a person inspecting a document, I want an empty Find what to have no hits, so that a faster search does not match everything.
42. As a person inspecting a document, I want a JSON document to still match nothing for an attribute name or a parent attribute, so that those conditions stay empty there.
43. As a person inspecting a document, I want a JSON value to stay the literal the cell shows, so that a faster search does not match the raw bytes of an escape.
44. As a person inspecting a document, I want text search to stay a second forward reading, so that it does not consult list-entry offsets or leave an index behind.
45. As a person inspecting a document, I want hits to still be able to appear while the scan is running, so that a search started early is not blocked on a private index.
46. As a person inspecting a document, I want a text search that is already within twice a sequential read of that file to be recorded as missing the half and to get no slower, so that the second reading is not replaced by an index to beat the disk.
47. As a person inspecting a document, I want the working set after the scan, before a large hit list, to grow by at most half, so that a faster outline is not a second copy of the document.
48. As a person inspecting a document, I want that bound to be against the current viewer on the same document, so that half of the file's size is not the allowance.
49. As a person inspecting a document, I want the working set at the end of the signing search to grow by at most half against the same search today, so that storing the hits does not become an excuse for a larger copy.
50. As a person inspecting a document, I want the largest list in the document, after the scan has finished, to expand in at most half the time it takes today, so that the first open of that list is held to the ratio.
51. As a person inspecting a document, I want that expand to show the visible entries and not to walk the rest of the document, so that the half is not bought by realizing every entry.
52. As a person inspecting a document, I want two lists with the same count to use the one that appears first, so that the signed list is determined.
53. As a person inspecting a document, I want a vertical scroll of one viewport, on an 800 by 600 viewport, to finish in at most half the time it takes today when the surface is taller than the viewport, so that moving down a long list is held to the ratio.
54. As a person inspecting a document, I want a horizontal scroll of one viewport, on that same viewport, to finish in at most half the time it takes today when the surface is wider than the viewport, so that moving across a wide table is held to the ratio.
55. As a person inspecting a document, I want a scroll that has less than a viewport left to move to the end of that axis, so that the last portion of the surface is still the gesture that is timed.
56. As a person inspecting a document, I want an axis that does not overflow to get no slower, and not to be required to halve, so that a surface which already fits is not held to a ratio it cannot show.
57. As a person inspecting a document, I want dragging the first data column ten character widths wider to finish in at most half the time it takes today, so that one drag of that column is held to the ratio.
58. As a person inspecting a document, I want every other column drag to get no slower, so that signing one column does not leave the rest of the table behind.
59. As a person inspecting a document, I want a column fit of that same first data column to finish in at most half the time it takes today, so that the long fit on a large list is held to the ratio.
60. As a person inspecting a document, I want that column fit to start with the list already expanded, so that the expand is not counted inside the fit.
61. As a person inspecting a document, I want the fitted width to be the width column fit produces today, so that the half is not bought by skipping entries.
62. As a person inspecting a document, I want a column fit to read every entry when no cell has yet reached five hundred characters, so that a list of short cells still decides the width from all of them.
63. As a person inspecting a document, I want a column fit to stop growing once a cell is already five hundred characters, so that the early stop stays where it is.
64. As a person inspecting a document, I want a column fit that still takes seconds after the half to be acceptable, so that the ratio is the bar and a one-second finish is not.
65. As a person inspecting a document, I want each pause in a column fit to stay about a single slice, so that the window keeps accepting input through a fit of hundreds of thousands of entries.
66. As a person inspecting a document, I want the working set during that column fit to grow by at most half against the same fit today, so that the fit does not keep the text it measured.
67. As a person inspecting a document, I want a document with no list to expand its first expandable cell, scroll both ways, drag its value column, and column-fit that value column, each under the same rules, so that a record table is not skipped.
68. As a person inspecting a document, I want expand, scroll, drag, and column fit on a small document to halve on the stopwatch as well, so that a gesture of a few milliseconds is not excused.
69. As a person inspecting a document, I want those clocks to exclude painting, so that paint jitter does not decide the half.
70. As a person inspecting a document, I want a slower paint to count as a regression, so that moving the cost into the drawing does not pass.
71. As a person inspecting a document, I want three successive runs in one process, and the median, so that one cold outlier does not decide the half.
72. As a person inspecting a document, I want the comparison made on a Release build against the current viewer, so that a debug build is not the bar.
73. As a person inspecting a document, I want expand, scroll, and drag not to keep a second copy of the surface, so that the half is not a cached picture of the document.
74. As a person inspecting a document, I want the viewer to stay on one thread for this work, so that the half is not bought by splitting the reading before the single reading has given up what it can.
75. As a person inspecting a document, I want an embedded table to stay inside its cell, so that a faster surface is not a tree of indented rows.
76. As a person inspecting a document, I want one scroll surface, so that the faster gestures do not add a scrollbar per table.
77. As a person inspecting a document, I want the viewer to stay read-only, so that a faster open still cannot change the document.
78. As a person inspecting a document, I want choosing a hit to expand the embedded tables on its path and scroll that cell into view, so that reveal still works.
79. As a person inspecting a document, I want each open document to stay where I left it when I switch away and back, so that a faster scan is not run again on return.
80. As a person inspecting a document, I want opening one entry, or jumping to the last entry of a long list, to stay proportional to that entry, so that a faster scan does not make one cell read the whole list.
81. As a person inspecting a document, I want the floors on a column drag to stay where they are, so that a faster drag does not pull a column under its minimum.
82. As a person inspecting a document, I want a shared right edge to keep moving the columns it moves today, so that a faster layout does not change which widths are shared.
83. As a person inspecting a document, I want mixed content to stay a `#text` field beside the child elements, so that a faster reading does not drop that text.
84. As a person inspecting a document, I want `{}` and a collapsed `Name[n]` to stay out of value search, so that a faster search does not treat a collapsed label as a value.

## Implementation Decisions

- The viewer session stays the surface for every clock in this spec. Opening a document, pumping the scan, searching, expanding, moving the viewport, dragging a column, and column fit do not gain a new operation. The desktop shell keeps calling scan slices and column-fit slices so the window can take input. Tests and the timing clocks talk to the session. Painting is outside the clock.
- Speed is less temporary work on the scan's forward reading and on the text search's forward reading, and less temporary work on the session operations that build the surface for one gesture. The outline's contents stay what they already are: field name, kind, and place, list count and columns, and a file offset per list entry. A closed record keeps that outline and no second body.
- JSON and XML stay two grammars and two readings. They are not merged. Each reading must be general across every document that grammar already accepts, at every depth. A mechanism that only recognizes the employee documents, the harness documents, or the `.vxf` documents is not acceptable. A mechanism that applies to any repeated sibling, at whatever depth, is general.
- Text search remains a second forward reading. It stores a path and the matched stretch, in document order. It does not read list-entry offsets and it does not leave an index. Every text-search condition stays on that one search. Only the default condition is required to halve. The others must keep today's hits and must not get slower.
- The signing Find what is three characters, chosen once per document and then fixed. It must produce at least one hit and at most two thousand under the default text search. A three-character sequence outside that hit range is not the signing search.
- One slice of scan work, and one slice of column fit, still returns within the existing short bound, on the order of a frame and well under a tenth of a second. The half is not bought by lengthening a slice.
- Column fit of a list column still walks every entry until each has been measured or the running width has reached the five-hundred-character cap. The width it applies is that result. Sampling entries, or stopping at a fixed count of entries, is not the way to the half.
- The viewport for the scroll clocks is 800 by 600. Vertical scroll moves one viewport down. Horizontal scroll moves one viewport across. A remainder shorter than a viewport moves to the end of that axis. An axis whose content already fits is not required to halve. It must not get slower.
- The signed gestures, after the scan has finished, use the list with the greatest entry count, or the earliest such list when counts tie. The order is: expand that list once; scroll vertically; scroll horizontally; drag the first data column ten character widths wider; column-fit that same column. The expand is not part of the fit clock. A document with no list uses the open record's value column for the drag and the column fit, one scroll on each axis, and the first expandable cell for the expand. Other columns must not get slower.
- The first data column is the first column of that list table that is not the entry index.
- Retained memory is compared per operation with the current viewer on the same document. The peak working set after the scan and before a large hit list, at the end of the signing search, and during the signing column fit, may each grow by at most half. The fit does not keep the text it measured. Expand, scroll, and drag do not keep a second surface.
- A sequential read of the same file on the same machine is the disk comparison. A scan median, or a signing-search median, already within twice that read is recorded as missing the half and must not get slower. No index and no skipped bytes are added to change that recording.
- The existing check that a local document of about 500MB finishes scanning within fifteen seconds stays a regression floor. This spec does not replace it with a machine-specific number of seconds. The half is a ratio against the current viewer, measured on the timing set.
- This work stays single-threaded. Extra threads are out until a later decision says the single reading has nothing left to give up.
- ADR 0049 is the decision. It adds the ratio that ADR 0044 declined to set, and it allows the working set to grow by half. It does not reopen the resident index, the refusal to let search read list-entry offsets, or the refusal of a walk specialized to the samples.

## Testing Decisions

- One seam: the viewer session. A test opens a document, pumps, searches, expands, moves the viewport, drags a column, column-fits, and reads the outline, the hits, and the resulting width. It does not call a parser or a search walker directly, and it does not assert which internal technique ran. The desktop shell is not in the clock. A painted frame is not the pass condition.
- A good test checks behavior a person can observe through the session: scan progress, the outline's names, kinds, counts, and columns, an entry opened by its stored place, hit path, match offset, match length, and document order, the width a column fit applies, and that a slice returns inside the existing short bound. It does not check allocation counts. Less temporary allocation is how the work gets faster, not a result of its own.
- Prior art is the session tests for a large scan, for text-search conditions, for expand and for a slice of a long list, for column fit, and for a JSON document. New behavior tests belong beside those, at the same seam. The yield bound already covered for a scan slice stays the pattern for a column-fit slice.
- The half is not encoded as a seconds assertion in the suite. The timing documents live outside the repository and the ratio depends on the machine. Acceptance is a same-machine measurement, through the session, on the protocol in the implementation decisions: Release, three successive runs in one process, the median, viewport 800 by 600. The baseline is the current viewer, taken before this work changes the reading. The work is done when each signed clock on each timing document is at most half that baseline, except a clock that the disk exit or a non-overflowing axis has excused, which must not be slower. Those times are appended to this spec. The documents are not committed.
- A retained-memory note on that measurement records the three working-set points. A unit test may bound managed memory after a scan by the outline and the hits. It does not assert the process working set, which includes the runtime.
- Generated documents keep today's outline and today's hits for a repeated list, distinct field names, nested repetition, UTF-8 text, a namespace, and an internal entity, and for a JSON array of objects. A column fit of a long list of short cells still applies the width produced by measuring every entry.

## Out of Scope

- Extra threads, a resident search index, or teaching text search to read list-entry offsets.
- A walk that keys off the names, the depth, or the schema of one timing document.
- Sampling a column fit, or any fitted width that is not the width column fit produces today.
- A requirement that a column fit of a huge list finish within one second.
- Halving a scroll axis that does not overflow.
- Halving starts-with, entire-cell, Match case off, or a field, parent, grandfather, or parent-attribute search. Those must keep today's hits and must not get slower.
- Halving every column. The signed drag and the signed column fit are one column. The others must not get slower.
- Putting process start, the file dialog, or painting into the clock.
- Replacing the fifteen-second scan floor with a tighter number in the suite.
- Merging the JSON and XML grammars, or choosing the grammar from the file name.
- Changing which siblings form a list, how a record table is oriented, how a cell expands, how a hit is revealed, how many scroll surfaces a document has, or the floors and shared edges of a column drag.
- Changing the find window, the results window, or the wording of paths.
- A scalar cell that decodes its whole value during the scan.
- Native AOT, and shipping or testing builds other than Windows.
- Committing the timing documents, or repairing a malformed region.
- Editing or saving the document.

## Further Notes

- ADR 0049 records the decision. ADR 0044 still rejects a resident index, still rejects a walk specialized to the samples, and still keeps text search off the list-entry offsets. ADR 0024 and ADR 0026 are the outline this work must not grow into a second body. ADR 0025 is the responsive scan and the fifteen-second floor. ADR 0027 is the text search the hits must keep. ADR 0009 keeps expansion inside the cell. The external subset stays unread.
- Glossary names in `CONTEXT.md` are the names to use. This spec uses Document, Scan, Scan fraction, Remaining time, Outline, Text search, Find what, Hit, Path, List table, Record table, Entry, Cell, Embedded table, and Column fit. Column fit was added for the double-click on a column edge.
- The timing set is every file under `C:\D\OPC\data`: the employee JSON and XML documents, the three `.vxf` documents, the harness XML documents, and the EDM JSON document. They are not fixtures.
- On one machine, before this spec, a Release session measurement of four of those files, two runs, with a rare Find what rather than the signing three-character Find what, was: employees-50-MB.json scan about 0.60s and search about 0.50s, employees-50-MB.xml scan about 1.6s and search about 0.71s, the smallest harness XML scan about 0.19s to 0.27s and search about 0.21s to 0.25s, employees-200-MB.json scan about 1.15s and search about 1.44s. Sequential reads of those files were under 0.11s. Peak working set after the scan was about 28MB to 49MB. Those numbers are orientation. They are not the signing baseline. The signing baseline is taken with the protocol in this spec before the reading changes. The 1GB and 2GB documents were not in that measurement.
- An earlier pass over the employee documents and the `.vxf` documents is recorded on the resolved general-walk spec. This spec does not reuse those search strings, because the signing Find what is three characters with a bounded hit count.
