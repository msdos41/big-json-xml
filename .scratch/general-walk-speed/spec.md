# Faster general scan and search

Status: resolved

## Problem Statement

A person opens a large XML or JSON document and waits while the viewer is scanning. A text search waits again for another forward reading of the same document. On a local disk the wait is long enough to matter: a document of about 2GB can spend well over a minute scanning and the better part of a minute searching. The outline left when the scan ends is small, on the order of a few tens of megabytes of working set no matter how large the document was. The time is spent building temporary objects that the outline does not keep.

The documents used to notice this are only examples. A repeated list, a tree of distinct names, a deep nest, UTF-8 text, namespaces, and entities are all documents the viewer already opens. A faster reading that only understands one of those shapes is the wrong fix.

## Solution

Scan and text search both finish sooner, on the forward reading they already do. The same reading is used for every document the viewer already opens, at every depth. A repeated list and a tree of distinct names may still take different amounts of time, because a list keeps an entry offset and a closed record keeps an outline without a second body.

When the scan ends, the working set stays in the band it already occupies: a few tens of megabytes, with the managed heap growing from list-entry offsets and from the hit list. There is still no search index. A hit is still a path and the stretch that matched, in document order, for every text-search condition. The window still accepts input while the scan runs.

No new number of seconds is promised. After the change, the example documents are timed again. The change is acceptable when those times are lower, the memory band and the hits are unchanged, and a large document of a different shape is not slower than it is today.

## User Stories

1. As a person inspecting a document, I want a large document to finish scanning sooner, so that I can read it without a wait that grows worse than the size of the document.
2. As a person inspecting a document, I want a text search of that document to finish sooner, so that I am not paying for a second slow reading.
3. As a person inspecting a document, I want the scan and the text search to get faster together, so that searching does not keep the old cost after the scan has improved.
4. As a person inspecting a document, I want the faster reading to be the one the viewer already uses, so that I do not learn a second way to open or search.
5. As a person inspecting a document, I want an XML document and a JSON document to keep their own grammars, so that a faster reading does not treat the two as one language.
6. As a person inspecting a document, I want the file name to stay irrelevant, so that a document whose name does not end in `.xml` or `.json` still opens from its first byte.
7. As a person inspecting a document, I want a repeated list and a tree of distinct field names to use that same reading, so that the fast path is not reserved for one sample.
8. As a person inspecting a document, I want a list nested inside another list to use that same reading, so that only the outermost list is not the one that gets faster.
9. As a person inspecting a document, I want a record nested many levels down to use that same reading, so that a fixed depth is not required for the document to be usable.
10. As a person inspecting a document, I want the reading to ignore the element names of the example documents, so that a later document with different names is not left on the slow path.
11. As a person inspecting a document, I want UTF-8 text to be read on that same reading, so that a non-ASCII character does not force the document onto a slower shape-specific path.
12. As a person inspecting a document, I want a namespace to keep its displayed prefix and its identity, so that a faster reading does not flatten qualified names.
13. As a person inspecting a document, I want an internal entity to expand as it does today, so that a faster reading does not skip or double that text.
14. As a person inspecting a document, I want an external entity to stop the scan as it does today, so that a faster reading does not fetch outside the document.
15. As a person inspecting a document, I want mixed content to stay a `#text` field beside the child elements, so that a faster reading does not drop or merge that text.
16. As a person inspecting a document, I want indentation made of spaces, tabs, carriage returns, and line feeds to stay out of the outline, so that a pretty-printed document does not gain blank fields.
17. As a person inspecting a document, I want the scan to leave the same outline, so that names, kinds, list counts, and columns do not change just because the reading got faster.
18. As a person inspecting a document, I want each list entry's place to stay after the scan, so that I can still open any entry without reading the others.
19. As a person inspecting a document, I want a closed record to keep its outline and no second body, so that a faster scan does not start retaining a parsed copy per element.
20. As a person inspecting a document, I want collapsing a list or a record to keep what the scan stored, so that the next open does not walk the document again.
21. As a person inspecting a document, I want the managed memory after the scan to stay small as the document gets larger, so that a multi-gigabyte document does not occupy a multi-gigabyte outline.
22. As a person inspecting a document, I want that retained memory to grow with the outline and with the hits, so that more list entries or more hits account for the growth.
23. As a person inspecting a document, I want the working set after the scan to stay on the order of a few tens of megabytes, so that the viewer remains the size it is today.
24. As a person inspecting a document, I want a text search to add memory for the hits and not for a second copy of the document, so that a search does not undo the small outline.
25. As a person inspecting a document, I want no search index left behind after the scan, so that searching still means a forward reading rather than a stored index.
26. As a person inspecting a document, I want the window to keep accepting input during the scan, so that a faster reading does not freeze the viewer until the end.
27. As a person inspecting a document, I want each pause in the scan to stay short, so that scrolling and expanding are still handled many times a second.
28. As a person inspecting a document, I want the scan fraction and the remaining time to keep updating, so that a faster reading does not hide how far the scan has reached.
29. As a person inspecting a document, I want the scan to run to the end of the document, so that a faster reading is not a reading that stops early.
30. As a person inspecting a document, I want a scan that stops on an ill-formed byte to stop where it stops today, so that the outline built before the stop is the same outline.
31. As a person inspecting a document, I want a JSON document that is empty, whitespace only, or two values to be refused as it is today, so that a faster reading does not start accepting those files.
32. As a person inspecting a document, I want a repeated JSON key to stop the scan as it does today, so that a faster reading does not invent a second field.
33. As a person inspecting a document, I want a text search to find the same hits, so that a faster search does not add or drop matches.
34. As a person inspecting a document, I want those hits in document order, so that Find next still walks the document from front to back.
35. As a person inspecting a document, I want each hit to keep its path and the stretch that matched, so that revealing the hit still opens the same cell.
36. As a person inspecting a document, I want two matches in one field to stay two hits, so that a faster comparison does not collapse overlapping matches.
37. As a person inspecting a document, I want hits to appear while the scan is still running, so that I can read early matches before the outline is finished.
38. As a person inspecting a document, I want an empty Find what to have no hits, so that a faster search does not match everything.
39. As a person inspecting a document, I want Match case to keep its meaning, so that turning it off is still ordinal case-folding and turning it on is still exact.
40. As a person inspecting a document, I want contains, starts-with, and entire-cell to stay the three ways a value is tested, so that a faster comparison does not add a wildcard or a fourth mode.
41. As a person inspecting a document, I want element name, attribute name, and value to stay independently switchable, so that turning one off still excludes only that text.
42. As a person inspecting a document, I want a field name, a parent element name, a grandfather element name, and a parent attribute name to keep narrowing hits, so that a faster search does not ignore a condition I set.
43. As a person inspecting a document, I want an empty name limit to add no limit, so that a blank box does not suddenly exclude hits.
44. As a person inspecting a document, I want comments and processing instructions to stay out of search, so that a faster search does not start matching them.
45. As a person inspecting a document, I want a JSON attribute name and a JSON parent attribute to match nothing, so that those conditions stay empty on a JSON document.
46. As a person inspecting a document, I want a JSON value to stay the literal the cell shows, so that a faster search does not match the raw bytes of an escape instead of the decoded text.
47. As a person inspecting a document, I want `{}` and a collapsed `Name[n]` to stay out of value search, so that a faster search does not treat a collapsed label as a value.
48. As a person inspecting a document, I want a hit on a field name to stay that field, so that revealing it still lands on the named field.
49. As a person inspecting a document, I want the scalar cell to keep showing a window, so that a faster scan does not decode a multi-megabyte value into one string.
50. As a person inspecting a document, I want opening one entry to stay proportional to that entry, so that a faster scan does not make the last entry read the whole list.
51. As a person inspecting a document, I want a document larger than the examples to take longer because it is larger, so that the reading is not abandoned at a fixed size.
52. As a person inspecting a document, I want a local disk that can deliver the bytes quickly to show the cost of the reading, so that the wait I see is the scan and the search rather than a remote disk.
53. As a person inspecting a document, I want the existing time bar for a document of about 500MB to keep holding, so that a change aimed at other documents does not make that scan late.
54. As a person inspecting a document, I want that bar to hold for both a repeated list and distinct field names, so that one of those shapes is not allowed to regress.
55. As a person inspecting a document, I want the example documents remeasured after the change, so that "sooner" is a time I can compare with the time I saw before.

## Implementation Decisions

- The viewer session stays the surface. Opening a document, pumping the scan, searching, reading a record, reading a list, reading a scalar window, and reading hits do not gain a new operation.
- Speed is removed temporary work on the scan's forward reading and on the text search's forward reading. The outline's contents stay what ADR 0024 and ADR 0026 already require.
- JSON and XML stay two grammars and two readings. They are not merged into one tokenizer. Each reading must be general across the documents that grammar already accepts.
- The reading must not branch on an element name, a fixed depth, or the shape of the example documents. A mechanism that applies to any repeated sibling name, at whatever depth that repetition occurs, is general. A mechanism that only recognizes the example documents is not.
- A character the grammar already accepts, including a non-ASCII UTF-8 character, must not by itself force a heavier reading or a larger retained outline. Entities, namespaces, mixed content, and the existing stop rules stay correct when that character is handled.
- Text search still stores a path and the matched stretch. It does not consult list-entry offsets, and it does not leave an index. It still runs interleaved with the scan, and it still runs when started after the scan has finished.
- Every text-search condition stays on that one search. Contains, starts-with, and entire-cell must agree on hit identity, including overlapping matches. Name limits and the element, attribute, and value switches must not grow a separate reading.
- One slice of scan work still returns quickly enough that the window can take input many times a second. The existing bound, a slice well under a tenth of a second, stays.
- The existing check that a local document of about 500MB finishes scanning within fifteen seconds stays the regression floor for both a repeated list and distinct names. This spec does not replace it with a tighter number.
- Retained results after the scan are the outline and, once a search has run, the hits. Temporary objects created while reading are not part of that result. ADR 0044 records this bound.

## Testing Decisions

- One seam: the viewer session. Tests open a document, pump, search, and read the outline and the hits. They do not call a parser or a search walker directly, and they do not assert which internal technique ran.
- A good test checks behavior the person can observe: progress, the outline's names, kinds, counts, and columns, entry places by opening an entry, hit path, match offset, match length, and document order. A slice's wall time and the existing fifteen-second bar are observable limits and stay.
- Prior art is the session tests for a large scan, for text-search conditions, and for a JSON document. New tests belong beside those, at the same seam.
- Generated documents cover a repeated list, distinct field names, repetition nested more than one level deep, UTF-8 text, a namespace, and an internal entity. JSON covers an array of objects and a scalar literal. Each of these must keep today's outline and today's hits.
- The example employee documents and the `.vxf` documents are a timing corpus, not fixtures. They are not committed. After the change they are remeasured by hand, and the times are appended on this spec. The suite does not encode those times as a new bar.
- A retained-memory check, if added, bounds managed memory after a scan by the outline and the hits. It does not assert the process working set, which includes the runtime and is not stable in a unit test.
- Tests do not require a particular allocation count during the reading. Less temporary allocation is how the reading gets faster. The locked results are the outline, the hits, the slice bound, and the existing fifteen-second bar.

## Out of Scope

- A new seconds target for the example documents or for a 2GB document.
- A resident search index, or teaching text search to read list-entry offsets.
- Raising the retained outline so that a later search can skip its forward reading.
- Merging the JSON and XML grammars, or choosing the grammar from the file name.
- Changing which siblings form a list, how a record table is oriented, how a cell expands, how a hit is revealed, or how many scrollbars the document has.
- Changing the find window, the results window, or the wording of paths.
- A scalar cell that decodes its whole value during the scan.
- Native AOT, and shipping or testing builds other than Windows.
- Committing the timing corpus, or repairing a malformed region.
- Editing or saving the document.

## Further Notes

- ADR 0044 is the decision this spec implements. ADR 0024 keeps entry offsets and rejects a full-document element index. ADR 0026 keeps a closed record's outline without a second body. ADR 0025 is the fifteen-second regression floor and the responsive scan. ADR 0029 keeps the two grammars apart. ADR 0027 is the text-search behavior the hits must keep.
- Glossary names in `CONTEXT.md` are the names to use. This spec uses Document, Scan, Outline, Viewer, Text search, Find what, Hit, Path, Field name, Parent element name, Grandfather element name, Parent attribute name, List field, Entry, and Scalar field.
- The timing corpus that motivated the work is local employee JSON and XML documents of about 50MB, 200MB, and 1GB, plus three `.vxf` documents of about 500MB, 1GB, and 2GB that open as XML. On one machine, before this work, the 2GB document scanned in about 77 seconds and a text search for a short literal took about 49 seconds. The scan allocated on the order of 95GB and retained about 11MB of managed heap. Those numbers are the baseline to beat. They are not a new acceptance bar.
- `CONTEXT.md` did not change. No new domain term was introduced, and none of the existing definitions moved.

## Remeasured

Same machine, through the viewer session, after this work. Employee files were searched for `Heidi`. The `.vxf` files were searched for `DELight`, which occurs only in a processing instruction and a comment, so the hit list is empty. Times are wall time. Working set is the process working set after a forced collection. These numbers are not a suite bar.

| Document | Scan | Search | Hits | Managed after scan | Working set after scan |
| --- | --- | --- | --- | --- | --- |
| employees-50-MB.json | 0.57s | 0.50s | 84 | 1.6MB | 29MB |
| employees-200-MB.json | 1.15s | 1.61s | 315 | 2.4MB | 30MB |
| employees-1-GB.json | 4.24s | 7.07s | 1669 | 5.6MB | 38MB |
| employees-50-MB.xml | 1.53s | 0.77s | 84 | 1.9MB | 35MB |
| employees-200-MB.xml | 4.41s | 2.56s | 315 | 2.7MB | 39MB |
| employees-1-GB.xml | 19.83s | 11.90s | 1669 | 5.8MB | 43MB |
| test_500mb.vxf | 4.48s | 7.75s | 0 | 3.8MB | 40MB |
| test_1gb.vxf | 8.95s | 16.33s | 0 | 6.1MB | 41MB |
| test_2gb.vxf | 20.15s | 37.49s | 0 | 10.1MB | 46MB |

The 2GB document was the baseline of about 77s to scan, about 49s to search, about 95GB allocated, and about 11MB retained. This reading scanned it in 20.15s and searched it in 37.49s, allocated about 10GB, and retained 10.1MB. Working set after every scan stayed inside a few tens of megabytes. Every `Heidi` hit on the 50MB and 200MB employee files, and on both 1GB employee files, matched the path, offset, and length from commit `02644c1`. The existing fifteen-second suite bar still passes.
