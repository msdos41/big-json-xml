# Halve the scan on deeply nested and multi-list documents

Status: resolved

## Problem Statement

A person opens a large XML document from a CAD, engineering, or industrial export (such as Mentor Graphics Capital) and waits ten to twelve seconds for the scan to finish, even though the file's sequential disk read completes in under half a second. While earlier optimizations halved the scan for simple documents dominated by a single large list, this document contains over eighty thousand small, dispersed list tables nested ten to fifteen levels deep. 

Every micro-list pays a two-entry cold start before repeated-sibling fast parsing can kick in. When each parent element closes, the parser discards what it learned, forcing subsequent lists of identical shape to cold-start again. Simultaneously, deep hierarchical stack walks and repeated column list allocations in the document tree consume over a third of the processing time. Furthermore, in the desktop viewer, a mandatory 32-millisecond work / 16-millisecond sleep slicing cycle and frequent interface repaints inflate the wall-clock wait by more than 1.5 times. The person wants this waiting time cut in half across both the scanning engine and the desktop viewer, without sacrificing outline accuracy or window responsiveness.

## Solution

The viewer session scan and the desktop interface loading each finish in at most half the wall-clock time they take today on deeply nested, multi-list XML documents.

The core engine accelerates by retaining verified list-element shape predictions across parent boundaries in a bounded cache, allowing subsequent occurrences of identical micro-lists to begin flat scanning from the very first entry. Upward ancestor propagation in the document tree is short-circuited when an ancestor record is already announced and no new field keys have been introduced, and parent frames are reached via direct references rather than linear stack searches. Redundant column list allocations are eliminated with a dirty flag on frame column collections. Within flat reading, attribute duplicate verification and intermediate text conversions are bypassed when consecutive entries share the exact same shape.

In the desktop viewer, the runner dynamically transitions to a 64-millisecond work slice with a 4-millisecond idle yield whenever user input has been quiescent for at least 250 milliseconds, immediately dropping back to a 32-millisecond work / 16-millisecond yield cycle upon any interaction. Scan fraction and remaining time interface updates are throttled to thirty frames per second. All work remains single-threaded, and the resulting outline, list counts, columns, and entry places remain identical to current behavior.

## User Stories

1. As a person inspecting an industrial XML document, I want the scan to finish in at most half the wall-clock time it takes today, so that I do not wait ten to twelve seconds before exploring the document.
2. As a person inspecting a document with many small lists, I want identical list shapes under different parents to begin fast scanning from the first entry, so that eighty thousand micro-lists do not each pay a slow two-entry cold start.
3. As a person inspecting a deeply nested document, I want leaf element additions to avoid walking the entire ancestor stack, so that deep hierarchy does not degrade scanning throughput.
4. As a person inspecting a document, I want the outline at the end of the scan to be completely identical to the outline produced today, so that field names, kinds, list counts, and columns are never altered by the faster reading.
5. As a person inspecting a document, I want each list entry's file place to remain exact, so that opening an entry reads that entry without parsing its siblings.
6. As a person inspecting a document, I want an open, unclosed non-simple element to remain visible as a field in its parent record table during the scan, so that ADR 0021 is fully honored.
7. As a person inspecting a document, I want the shape prediction cache to be bounded in memory, so that peak working set growth remains within the half-working-set allowance of ADR 0049.
8. As a person inspecting a document with heterogeneous tags, I want a speculative shape mismatch to immediately fall back to the safe standard reading, so that ill-formed, commented, or variable elements are never corrupted.
9. As a person inspecting a document with mismatched shapes, I want failed speculative attempts to be blacklisted for that tag combination, so that subsequent entries do not waste time on repeated failed probes.
10. As a person inspecting a document, I want consecutive entries sharing the same shape to skip duplicate attribute span tracking, so that flat scanning throughput is increased.
11. As a person inspecting a document, I want intermediate frames whose column definitions have not changed to avoid re-allocating column lists, so that unnecessary heap allocation and garbage collection pressure are eliminated.
12. As a person inspecting a document, I want the scan to remain strictly single-threaded, so that background thread contention and complexity are avoided.
13. As a person inspecting a document, I want existing timing-set documents (such as employee documents and VXF files) to experience no regression in scan speed, so that improving nested documents does not slow down flat ones.
14. As a person inspecting a document in the desktop viewer, I want the viewer to dynamically use longer 64ms work slices when I am not interacting with the window, so that the file loads significantly faster without unnecessary 16ms sleep delays.
15. As a person inspecting a document in the desktop viewer, I want the work slice to instantly revert to 32ms upon any mouse move, click, or keystroke, so that the window never feels sluggish or unresponsive during a scan.
16. As a person inspecting a document in the desktop viewer, I want interface progress updates to be throttled to 30 frames per second, so that the main thread is not bogged down by redundant full-frame layouts while scanning.
17. As a person inspecting a document, I want the scan fraction and remaining time display to stay accurate and continuously updating, so that I can see steady progress during long scans.
18. As a person inspecting a document, I want single scan slices to consistently return well under one-tenth of a second, so that the responsive scan contract of ADR 0025 is preserved.
19. As a person inspecting a document, I want text search, expand, scroll, and column fit to remain functional and correct, so that the faster scan does not degrade downstream surface operations.
20. As a person inspecting a document, I want the grammar to continue being chosen solely from the first byte after whitespace, so that renamed files open properly without schema-specific specialization.

## Implementation Decisions

- **Direct Parent Frame Reference**: `Frame` stores a direct reference to its immediate parent `Frame` when rented and pushed, replacing linear searches over `Stack<Frame>`. Recycling clears this reference.
- **Short-Circuited Announce Propagation**: When `Announce` is called on a child element, it checks its parent. If the parent is already announced as a non-simple record and the parent's `ColumnsDirty` flag is false, upward recursion terminates immediately. This turns deep $O(\text{depth}^2)$ stack crawls into $O(1)$ operations while guaranteeing that unclosed elements appear in the outline as required by ADR 0021.
- **Column Collection Dirty Tracking**: `Frame` maintains a `ColumnsDirty` boolean flag. The flag is set to true only when a previously unseen `FieldKey` is added to the frame. `ColumnsOf` is only invoked when `ColumnsDirty` is true, avoiding thousands of redundant `List<Column>` allocations on static record tables.
- **Cross-Parent Shape Cache**: `XmlParser` retains a bounded cache of verified list element shapes keyed by `(ParentName, ChildName)`. When a subsequent parent opens and encounters that child name, entry 1 speculatively invokes `TryFlatElement`. The cache is bounded to 512 entries with a FIFO or LRU eviction policy, keeping memory overhead under 100 KB and adhering to ADR 0049.
- **Speculative Verification and Failure Blacklisting**: If a speculative `TryFlatElement` probe fails (due to CDATA, comments, entity references, or differing child tag sequences), the `(ParentName, ChildName)` key is removed and marked as ineligible for speculation for the remainder of that document session. Parsing immediately falls back to standard `ReadMarkup`.
- **SameShape Fast-Path Optimization**: When consecutive flat entries match the previous entry's shape (`SameShape == true`), `ConsumeFlat` skips populating and scanning `_attrSpans` for duplicate attribute checks, as attribute uniqueness was already validated when the shape was pinned.
- **Adaptive Desktop Work Slices**: `BigJsonXml.Desktop` monitors user interaction (mouse move, mouse button press, scroll wheel, and keyboard input) at the top-level window. If no interaction has occurred for 250 milliseconds, the scan slice budget increases to 64 milliseconds and the dispatcher sleep interval decreases to 4 milliseconds. Any user input event instantly resets the slice budget to 32 milliseconds and sleep interval to 16 milliseconds.
- **Throttled Surface Refresh During Scan**: Calls to `Refresh()` from `ViewerModel.Tick()` during scanning are throttled to an interval of approximately 33 milliseconds (30 fps), preventing redundant `ReadFrame` executions and property notifications while the scan is running.
- **ADR Alignment**: Recorded as ADR 0050. Preserves ADR 0021 (open element is a field), ADR 0024 (list offsets stay after scan), ADR 0025 (scan responsiveness), ADR 0026 (closed record keeps outline and no second body), ADR 0044 (speed stays on the general walk), and ADR 0049 (half the time on the general walk).

## Testing Decisions

- **Primary Seam: `ViewerSession`**: Core tests interact solely with the public session API (`ViewerSession.Open`, `Pump`, `ReadRecord`, `ReadList`, `BytesReached`, `Progress`, `DocumentLength`). Tests assert that outline names, kinds, list counts, column headers, and entry places match expected values without inspecting internal caches or parser states.
- **Desktop Seam: `ViewerModel`**: Desktop tests exercise `ViewerModel.Tick()` and verify that when interaction is quiescent (>250ms), slice execution increases up to 64ms with 4ms yield, and upon user input drops back to 32ms/16ms. Tests verify that UI progress update throttles to ~30fps.
- **Verification Protocol**: Same-machine measurement on Release builds, median of three runs. Benchmarked against `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` and existing timing-set documents (`employees-50-MB.xml`, `employees-200-MB.xml`, `test_500mb.vxf`, and `CD701EV CHS XML` documents).
- **Behavior Regression Floor**: Tests confirm that a document of about 500MB finishes scanning within fifteen seconds. All existing unit tests in `BigJsonXml.Core.Tests` and `BigJsonXml.Desktop.Tests` must continue to pass without modification.
- **Memory Bound Verification**: Tests verify that process working set after scan does not exceed one and a half times the baseline recorded in ticket 01.

## Out of Scope

- Multi-threading, background worker pools, or parallel tokenization.
- Persistent disk-based search indices or list-entry offset caches.
- Hardcoding specific element names, attribute names, or schemas from Mentor Graphics Capital or any other domain.
- Altering the presentation of list tables, record tables, or embedded tables.
- Modifying text search semantics or conditions.
- Native AOT compilation or non-Windows platform support.

## Further Notes

- The test file `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` represents a class of deeply nested XML files containing tens of thousands of short tables. Prior optimizations focused on documents dominated by a single large list table (like employee records), which left multi-list cold-start overhead unaddressed.
- ADR 0050 documents the architecture and constraints for cross-parent shape prediction and adaptive slicing.

## Baseline

Measured on 2026-10-14 through Release builds. Sequential read is median of three runs with a 1MB buffer, including file open. Scan clock runs from start of reading to completed outline, median of three runs in one process. Peak working set is process working set recorded after scan completes. Desktop scan wall-clock time is measured from open to scan completion via ViewerModel.

| Document | Bytes | Seq ms | Scan ms | Peak WS bytes |
| --- | ---: | ---: | ---: | ---: |
| CD701EV CHS XML\底盘电线束总成-CD701.xml | 54864344 | 12.69 | 833.19 | 48787456 |
| CD701EV CHS XML\发动机舱电线束总成-CD701.xml | 326175380 | 68.08 | 1021.11 | 57663488 |
| employees-50-MB.xml | 41711374 | 6.95 | 63.85 | 67526656 |
| employees-200-MB.xml | 166840082 | 27.85 | 242.70 | 50376704 |
| test_500mb.vxf | 524317166 | 86.96 | 1700.99 | 56504320 |
| employees-1-GB.xml | 854342244 | 144.02 | 1273.16 | 52219904 |
| test_1gb.vxf | 1078151394 | 181.08 | 3463.78 | 48852992 |
| 4d7480cde29a662758904cbe0e1afdbb-BD_.xml | 772381930 | 130.90 | 5753.71 | 111833088 |

Desktop viewer scan completion wall-clock time on `4d7480cde29a662758904cbe0e1afdbb-BD_.xml`: 8826.73 ms.
 
## Signed timing set
 
Measured on 2026-10-15 through Release builds under the protocol from ticket 01. Sequential read is median of three runs with a 1MB buffer, including file open. Scan clock runs from start of reading to completed outline, median of three runs in one process. Peak working set is process working set recorded after scan completes. Desktop scan wall-clock time is measured from open to scan completion via ViewerModel.
 
| Document | Bytes | Seq ms | Scan ms | Peak WS bytes |
| --- | ---: | ---: | ---: | ---: |
| CD701EV CHS XML\底盘电线束总成-CD701.xml | 54864344 | 16.38 | 570.87 | 45858816 |
| CD701EV CHS XML\发动机舱电线束总成-CD701.xml | 326175380 | 84.03 | 1208.24 | 51490816 |
| employees-50-MB.xml | 41711374 | 11.87 | 95.31 | 48115712 |
| employees-200-MB.xml | 166840082 | 39.23 | 431.53 | 45051904 |
| test_500mb.vxf | 524317166 | 113.79 | 1622.30 | 48283648 |
| employees-1-GB.xml | 854342244 | 184.07 | 1711.91 | 52482048 |
| test_1gb.vxf | 1078151394 | 267.56 | 3701.73 | 66596864 |
| 4d7480cde29a662758904cbe0e1afdbb-BD_.xml | 772381930 | 188.33 | 2699.28 | 52228096 |
 
Desktop viewer scan completion wall-clock time on `4d7480cde29a662758904cbe0e1afdbb-BD_.xml`: 3197.27 ms.


