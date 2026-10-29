# Background search with a byte prefilter

Status: ready-for-human

## Problem

After the scan, search parsed the whole file again on the window's pump, at about 3 s per GB. An XML drain held one pump call for the whole search (2.4-2.6 s with the window frozen). JSON searches took 36 ms ticks for about 3 s. Searching the 512 MB scalar array took about 24 s, with 3-4 s per tick.

## Solution

- After a completed scan, `ViewerSession.Search` starts a `BackgroundSearch` with its own reader (`IReaderSource.OpenReader`) and its own `XmlDocument` and searcher. `Pump` drains hits whose paths are final (`StableCount`) into `Hits`. A document up to 4 MB is searched at once on the caller.
- `SearchSkip` + `CandidateFinder`: a vectorized byte search finds the next place a hit could start; the searcher counts the entries before it from the list offsets and moves the parser to the entry holding the candidate (`XmlParser.JumpTo`, `JsonParser.SeekArrayItem`). XML root offsets come from `EntryTagScan`, used only when the tag count equals the scan's count.
- Desktop: a tick pumps once while the search thread works and refreshes only when hits arrive; Find Next waits on the thread.
- ADR 0053.

## Measured

Box (Linux, 8 cores), Release, `/workspace/bench2` search mode, warm cache. Total time to search end; second run; ticks simulate the window (pump up to 32 ms, 4 ms idle).

| File, needle | Before | After |
| --- | --- | --- |
| x1g.xml, hit in middle | 2.38 s, one 2.4 s tick | 0.58 s (first hit 0.30 s), max tick 0.1 ms |
| x1g.xml, hit at end | 2.37 s | 0.58 s |
| x1g.xml, no hit | 2.39 s | 0.42 s |
| j1g.json, middle | 2.82 s (first hit 1.43 s), ticks 36 ms | 0.23 s (first hit 0.13 s) |
| j1g.json, end | 2.81 s | 0.25 s |
| j1g.json, no hit | 3.0 s | 0.24 s |
| d1g.xml, middle | 2.57 s, one 2.5 s tick | 0.37 s (first hit 0.20 s) |
| d1g.xml, end | 2.49 s | 0.44 s |
| d1g.xml, no hit | 2.50 s | 0.26 s |
| t512m.json, middle | 24.2 s (first hit 12.3 s), 3-4 s ticks | 0.10 s (first hit 0.05 s) |
| t512m.json, end | 24.7 s | 0.10 s |
| t512m.json, no hit | 25.0 s | 0.10 s |

The first XML search in a document also counts the root entry tags once (about 0.3 s on x1g). Peak RSS stays 52-81 MB. Case-insensitive no-hit search (no prefilter): x1g 17.7-19.0 s before and 18.2-18.7 s after; j1g 12.1-12.4 s before and 14.4-14.5 s after, but ticks drop from 36 ms to under 2 ms.

Memory-mapped vs `RandomAccess.Read` for the byte search, warm cache: x1g 273 vs 265 ms, j1g 200 vs 206 ms, t512m 94 vs 94 ms. Not worth changing.

## Follow-up

- Case-insensitive ASCII prefilter (search both cases of the first byte).
- Run searches started during the scan on the thread too, once the scan front can be shared safely.
- XML lists below the root, and roots with more than one list, still parse every entry.
