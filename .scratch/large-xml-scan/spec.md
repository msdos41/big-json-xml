# Large XML scan

Status: ready-for-agent

## Problem Statement

A person opens a local XML document of about 500MB and the viewer stays on Scanning for many minutes. The window still accepts input, and the scan is still moving, but the document is not usable in any practical sense. The document they are opening is pretty-printed with Windows line endings and is mostly repeated sibling elements. A document of the same size made of distinctly named elements has to stay on the same scan.

## Solution

The scan of a local XML document of about 500MB finishes in about ten seconds of wall time. The window keeps accepting input throughout. The scan is still one forward reading of the whole document and leaves an outline: each field's name and kind, each list field's count and columns, and enough to open any entry without reading the other entries. A closed record has that outline and no second parsed copy of the element. Indentation made of spaces, tabs, carriage returns, and line feeds is not a field. JSON stays unopened.

## User Stories

1. As a person inspecting a document, I want a local XML document of about 500MB to finish scanning in about ten seconds, so that I can read it without waiting for the scan to run for minutes.
2. As a person inspecting a document, I want the window to keep accepting input while the scan runs, so that a large document does not freeze the viewer.
3. As a person inspecting a document, I want each pause in the scan to be short, so that scrolling and expanding are handled many times a second.
4. As a person inspecting a document, I want the document element's record table to appear before the scan ends, so that I can read the first fields immediately.
5. As a person inspecting a document, I want a list field's count to grow while those siblings are still being scanned, so that `Name[n]` is not withheld until the end of the document.
6. As a person inspecting a document, I want the scan to run to the end of the document, so that a machine that misses ten seconds still produces a complete outline.
7. As a person inspecting a document, I want a document larger than about 500MB to take longer in proportion to its size, so that ten seconds is a bar for this size and not a point where the scan is abandoned.
8. As a person inspecting a document, I want the ten-second bar to apply when the bytes can be read from a local disk well inside ten seconds, so that the bar measures the scan rather than a slow or remote disk.
9. As a person inspecting a document, I want a list-shaped document and a document of distinctly named elements to open through the same viewer, so that the fast path is not reserved for one sample file.
10. As a person inspecting a document, I want the status to leave Scanning when the outline is complete, so that I know counts and columns are final.
11. As a person inspecting a document, I want the scan to leave an outline of each closed element, so that opening it later does not depend on a second parsed copy.
12. As a person inspecting a document, I want each field's name and kind to be in that outline, so that a record table is complete when the scan ends.
13. As a person inspecting a document, I want a list field's count and columns to be in that outline, so that `Name[n]` and the column headers do not require another pass.
14. As a person inspecting a document, I want to open any entry of a list without the viewer reading the other entries again, so that the end of a multi-million-entry list is cheap.
15. As a person inspecting a document, I want those entry places to be learned during the scan that is already walking the siblings, so that finishing Scanning is enough to jump.
16. As a person inspecting a document, I want collapsing a list to keep those entry places, so that putting the list away does not make the next jump read the list again.
17. As a person inspecting a document, I want collapsing a record to keep its outline, so that opening it again does not walk the rest of the document.
18. As a person inspecting a document, I want opening a record that contains a large list to use that list's stored count, columns, and entry places, so that expanding the record does not scan the list a second time.
19. As a person inspecting a document, I want a document of millions of distinct elements to finish scanning inside the same time bar, so that a different shape is not left on the slow path.
20. As a person inspecting a document, I want opening one nested record in that document to cost work proportional to that record, so that one field is not paid for with a full rescan.
21. As a person inspecting a document, I want a closed record to have no second parsed copy beside its outline, so that a distinct-name document does not retain a resident body per element.
22. As a person inspecting a document, I want a huge scalar to stay a window over its place in the document, so that a very large text value is not decoded into one string during the scan.
23. As a person inspecting a document, I want only the visible slice of a full value to be decoded, so that expanding a huge scalar does not allocate the whole value.
24. As a person inspecting a document, I want indentation made of spaces, tabs, carriage returns, and line feeds to be ignored, so that a pretty-printed document is not a stack of blank `#text` rows.
25. As a person inspecting a document, I want a carriage return alone between elements to be ignored, so that a gap that is only `\r` is not a field.
26. As a person inspecting a document, I want CRLF gaps between elements to be ignored, so that a document saved with Windows line endings matches the glossary.
27. As a person inspecting a document, I want `xml:space="preserve"` not to turn those gaps into fields, so that a data file stays compact.
28. As a person inspecting a document, I want spaces and line breaks that sit beside real text to be kept, so that significant whitespace inside a value survives.
29. As a person inspecting a document, I want an element's own non-whitespace text to appear as `#text` when that element also has other fields, so that mixed content is still visible.
30. As a person inspecting a document, I want a name seen twice to become a list and stay a list, so that promotion still happens once during the fast scan.
31. As a person inspecting a document, I want columns to stay in the order first discovered, with later fields appended, so that the table does not reshuffle as the scan speeds up.
32. As a person inspecting a document, I want only the visible rows of a long list to be realized, so that millions of entries do not create millions of controls.
33. As a person inspecting a document, I want the scroll range of an expanded list to grow while entries are still being found, so that I can read the first rows before that list is finished.
34. As a person inspecting a document, I want to jump to an entry the scan has already seen, so that the scrollbar works before Scanning ends.
35. As a person inspecting a document, I want to jump to an arbitrary entry after Scanning ends, so that the last entry of a huge list is a lookup.
36. As a person inspecting a document, I want a text search and a field search to read the document forward in the same manner as the scan, so that search is not left seeking the disk once per character.
37. As a person inspecting a document, I want hits to still be paths, so that search does not grow a full index of every element.
38. As a person inspecting a document, I want hits in document order for a character sequence, for `name = value`, and for `name ~ value`, so that the query language is unchanged.
39. As a person inspecting a document, I want hits to appear while the scan is still running, so that a search started early is not blocked on a slower private reader.
40. As a person inspecting a document, I want a search I start to be an extra pass, so that the ten-second bar stays the bar for the scan reaching its end.
41. As a person inspecting a document, I want a well-formedness error to stop the scan and keep the outline already built, so that a bad tail does not discard the good prefix.
42. As a person inspecting a document, I want external entities, external DTD subsets, and network fetches to still stop the scan, so that a faster reader is not a less safe reader.
43. As a person inspecting a document, I want internal entities expanded in the text I see and search, so that values still match declarations inside the document.
44. As a person inspecting a document, I want comments and processing instructions to stay absent, so that speed does not surface them as fields.
45. As a person inspecting a document, I want attribute names written with a leading `@`, so that the table language is unchanged.
46. As a person inspecting a document, I want a bare attribute to be an empty value and the scan to continue, so that one flag does not stop the rest of the document.
47. As a person inspecting a document, I want a small document's record tables, list tables, and embedded tables to look as they do today, so that the time work does not change ordinary documents.
48. As a person inspecting a document, I want one scroll surface for the document, so that the faster scan does not add scrollbars.
49. As a person inspecting a document, I want the viewer to remain read-only, so that a faster open still cannot change the document.
50. As a person inspecting a document, I want JSON to stay unopened, so that this work does not invent a JSON table.
51. As a person inspecting a document, I want a document that is one huge scalar to finish scanning quickly and show a window on that value, so that size in one text node is not a special failure.
52. As a person inspecting a document, I want an empty element to still appear, so that a present-but-empty tag is not dropped.
53. As a person inspecting a document, I want two spellings of the same namespace URI and local name to stay one field, so that a faster scan does not split columns.
54. As a person inspecting a document, I want choosing a hit to expand the embedded tables on its path and scroll that cell into view, so that reveal still works after the scan is fast.
55. As a person inspecting a document, I want a child to become a field once the scan has seen that it has an attribute or an element child, so that a long parent still appears before its end tag.

## Implementation Decisions

- This work stays inside the existing Viewer session and the thin desktop shell. The session owns the scan, the outline, list counts, entry places, search, and well-formedness stops. The shell draws the visible surface and forwards expand, collapse, scroll, search, and reveal. Tests for this spec talk to the session only.
- The product bar is ADR 0025. On a local disk that can deliver about 500MB well inside ten seconds, the scan reaches the end of the document in about ten seconds of wall time, and the window keeps accepting input. The scan is not cut off at ten seconds. A larger document may take longer. JSON stays out of scope (ADR 0002).
- A document on disk is read through a forward window for the scan and for search. Positioned reads remain for a scalar window, a slice of a full value, and opening an entry or a record at a place the outline already stores. The scan and the search must not seek the file once per character, and a peek or a whitespace check must not read those same bytes back from the disk one character at a time.
- Whitespace is the space, the tab, the carriage return, and the line feed. Text made only of those characters is not a field, including under `xml:space="preserve"`. Text that contains any other character is kept as written, including spaces and line breaks beside that character. The scan classifies whitespace from characters it is already visiting.
- The session yields to its caller after a bounded slice of work. One slice returns within a short bound, on the order of a frame and well under a tenth of a second, while a large document is still scanning. While a scan is in progress, the shell calls those slices so that idle time between slices is not larger than the slice itself, and so the window handles input at least several times a second. A scan whose own work fits in a few seconds therefore finishes in about ten seconds of wall time.
- The outline keeps each field's name, kind, and place. A list field also keeps its count, its columns, and a file offset for every entry. Those offsets are recorded as the scan sees each entry, and collapsing the list does not drop them (ADR 0024). This replaces the earlier rule that an offset list exists only while the list is expanded and may be discarded on collapse. Search does not read the offset list. A hit remains a path. There is still no full-document index of every element.
- A closed record keeps its outline and no second parsed copy of that element (ADR 0026). Opening it uses the outline. A list inside it uses the stored count, columns, and entry offsets rather than scanning that list again. The element the scan is currently inside can still be shown before its end tag.
- Per-element work during the scan stays small enough that a list on the order of ten million entries, and a document on the order of ten million distinct elements, both finish inside the time bar. The list case may retain on the order of one offset per entry. It must not retain a parsed body per entry. The distinct-name case retains the outline, not a resident body per element.
- Search uses the same forward reading as the scan. Hits are still reported in document order while the scan runs, for a character sequence, for `name = value`, and for `name ~ value`. The ten-second bar is the scan reaching the end of the document. A search is an extra pass the person asked for.
- Encoding, internal-entity expansion, the refusal of external entities and external DTD subsets, and stopping at a well-formedness error are unchanged. The outline built before the stop stays visible. The session still does not offer the document as a tree or as one string.

## Testing Decisions

- One seam: the Viewer session. Tests open a document, call the scan in slices, read a record, read a slice of a list, expand and collapse, and run a search. They observe whether the scan is still running or finished, field names, `Name[n]`, scalar text, hits, and wall time. The desktop shell is not a second seam. The shell schedule in the implementation decisions is still required.
- A good test asserts external behavior. It does not assert buffer sizes, private collections, or that a particular index type exists. It does assert costs a person would feel: one slice returns quickly while a large document is still scanning; after a long list is collapsed, reading an entry near the end does not take time or allocation proportional to reading the whole list again; opening one record in a large distinct-name document does not take work proportional to the whole document.
- Rule tests use small documents. They include a CRLF gap, a carriage return with no line feed, `xml:space="preserve"`, and a value that keeps whitespace beside real text. They include collapsing a list and then jumping, and collapsing a record and then opening it.
- The time bar is tested with generated documents of about 500MB written to a temporary file and opened through the file-backed session. An in-memory source alone cannot see the per-character seek. One document is repeated siblings. One document is distinctly named elements. Time spent building the file is not part of the measurement. The product target is about ten seconds. The test fails if that scan's wall time exceeds fifteen seconds, so a multi-minute scan cannot pass and a slightly loaded machine has a little room.
- Prior art is the core session tests for record tables, list promotion, jumps, whitespace, search, and a cheap reopen of a long list. Follow those tests' style: small documents for rules, one generated document when the cost is the point.
- Do not commit or require the 500MB sample used during design. A person may open that local document by hand. The automated bar is the generated file.
- Do not add a window test, a JSON test, or a multi-gigabyte fixture.

## Out of Scope

- Parsing or presenting JSON.
- Editing, saving, and any write-back of the document.
- XPath, and any query other than text search and `name = value` / `name ~ value` field search.
- A full-document index of every element, and teaching search to read list entry offsets.
- Dropping list entry places in order to save memory.
- Stopping or cancelling the scan when ten seconds elapse.
- Treating a network path or a disk that cannot deliver 500MB well inside ten seconds as the machine that must hit the bar.
- Changing which siblings form a list, how a record table is oriented, how a cell expands, or how a hit is revealed.
- A second scrollbar, a second window, or auto-opening the single large list under the document element.
- Native AOT, and shipping or testing macOS and Linux builds.
- Committing a 500MB document, or repairing a malformed region.

## Further Notes

- Glossary names in `CONTEXT.md` are the names to use. This spec uses Document, Viewer, Scan, Outline, Record table, List table, List field, Entry, Field, `#text`, Text search, Field search, Hit, and Path.
- ADR 0024 keeps a file offset for every list entry after collapse and supersedes ADR 0016's rule that offsets exist only while a list is expanded. ADR 0016's rejection of a full-document element index still stands. ADR 0025 is the ten-second bar. ADR 0026 is the closed record's outline. ADR 0002 still leaves JSON out. ADR 0018 is the whitespace rule; carriage return is included, as the glossary now states.
- The earlier viewer spec said a collapsed list may discard its offset index, and that 500MB is not an acceptance number. Both sentences are superseded for this work. The rest of that spec still describes the tables, the search language, and the stopped error.
- The sample that motivated the bar is a local CRLF harness document of 524,317,166 bytes, mostly repeated siblings, on the order of fifteen million elements. Opening it by hand is a useful check. It is not a fixture.
