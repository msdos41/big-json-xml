# Checkpoint index (step 3)

Status: ready-for-human

## What was measured first

Expanding and reading rows deep in a list already resume from stored entry offsets (ADR 0024, ADR 0052). `/workspace/bench2 expand`, Release:

| Gesture | Time |
| --- | --- |
| x1g.xml expand `/orders/order`, read rows 6005815-6005834 | 1 ms + 14 ms |
| d1g.xml `/design/cell[1774227]/pins/pin` rows | 13 ms to reach, 2 ms rows |
| n1g.xml (new nested file) `/db/orders/order` rows 2999981-3000000, then `lines` | 0 ms + 9 ms, 0 ms |
| n1g.json `/data/orders` last rows, then `lines` | 1 ms + 8 ms, 0 ms |
| Reveal of the last hit in x1g / d1g / j1g | 12 / 13 / 20 ms |

So a separate byte-interval checkpoint index would not speed these up. What still read the whole file was search: lists below the root, roots with several lists, needles with spaces or non-ASCII text. A nested XML file also held every hit until its parent record closed, so the first hit in the middle of n1g.xml showed up after about 2 s.

## Scope chosen

The list-entry offsets plus the finished outline serve as the checkpoints (ADR 0055):

- `PlanSkip` walks every record of the outline outside list entries (`WalkOutline`). XML: a list that is its parent's only child element, with no text and no namespace in scope, uses the scan's offsets (`_offsets`, count must match); the root still falls back to `EntryTagScan`. JSON: every list there. `SearchSkip.Find` looks the list up by parent path (XML) or bracket offset (JSON).
- `Searcher.TrySkip` works at any depth. `JsonSearcher` jumps in nested arrays.
- The needle is split into parts every hit holds byte for byte (`Parts`). The rarest part in a 1 MB sample from the middle of the file is the probe (`Rarest`).
- `SearchSkip.Target` pauses looking after 4 misses in a row, for 16 to 4096 entries.
- `Searcher.StableCount` holds a hit only while an unindexed step on its path lies in an open record whose outline lists that field (or has no outline).
- Small fix: a document up to 64 KB is searched to the end on the caller, so a busy machine cannot leave its hits for a later tick.

## Measured

Box (Linux, 8 cores), Release, warm cache, second run, `/workspace/bench2/run-n.sh`. n1g.xml (721 MB) and n1g.json (570 MB) come from `/workspace/gen/gen.py`: a root with meta, customers (1.5M), orders (3M, each with 3 lines) and audit (1M). QZXJW and QZXJV are written into orders 1500000 and 2999999.

| File, needle | Before | After |
| --- | --- | --- |
| n1g.xml QZXJW (middle) | 2.19 s, first hit 1.94 s | 0.31 s, first hit 0.19 s |
| n1g.xml QZXJV (end) | 2.22 s | 0.30 s |
| n1g.xml ZQX (none) | 2.19 s | 0.26 s |
| n1g.xml qzxjw, case ignored | 14.9 s | 0.40 s |
| n1g.xml 上海 (none) | 19.1 s | 0.35 s |
| n1g.json QZXJW | 8.1 s, first hit 4.1 s | 0.12 s |
| n1g.json QZXJV | 8.1 s | 0.12 s |
| n1g.json ZQX | 8.3 s | 0.12 s |
| n1g.json qzxjw, case ignored | 11.4 s | 0.13 s |
| n1g.json 上海 | 16.1 s | 0.12 s |
| x1g-ci.xml "QZXJW note" (middle) | 2.42 s | 0.38 s |
| x1g-ci.xml "QZXJV note" (end) | 2.41 s | 0.39 s |
| x1g-ci.xml "ZQX note" (none) | 2.39 s | 0.33 s |
| x1g-ci.xml 上海 | 23.3 s | 0.42 s |
| j1g-ci.json "QZXJW note" / "QZXJV note" / "ZQX note" | 2.34 / 2.37 / 2.62 s | 0.30 / 0.18 / 0.20 s |
| j1g-ci.json 上海 | 22.8 s | 0.19 s |
| x1g-ci / j1g-ci Entire cell "text" (a candidate in every entry) | 18.3 / 15.6 s | 17.4 / 13.8 s |

Searches that already jumped are unchanged (`run.sh`, `run-ci.sh`): for example x1g 0.53/0.55/0.39 s before and 0.53/0.49/0.37 s after; j1g case ignored 0.24/0.20/0.20 s before and 0.21/0.21/0.21 s after. Scan code is untouched; scan times are the same within noise (x1g 2.4-3.1 s, n1g.xml 2.5 s). Peak RSS for the n1g runs falls from about 140 MB to 54-58 MB, because the full reads are gone.

## Follow-up

- Byte-interval checkpoints, for: splitting a full read (dense hits, Entire cell over common text, a needle with no ASCII part and case ignored) across threads; searches started during the scan; documents whose bulk is not a list.
- XML parents with several kinds of child elements, text, or namespaces still read their entries.
- JSON and XML lists inside list entries still read their entries (they are small in the files measured).
