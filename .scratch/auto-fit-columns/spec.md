# Automatic column fit

Status: ready-for-human

Stacked on `perf/fast-column-fit` (PR #16): it uses that branch's fit rule (`ListWidth`, `FitColumnCharacters`) and `MeasureCell`.

## What it does

- `LayoutList` notes each list it draws (`NoteDrawnList`). A list is queued once.
- `ViewerModel.Tick` calls `ViewerSession.AdvanceAutoFit(8)` for the active tab after the pending double-click fit. It does nothing while the scan runs.
- One run (`AutoRun`) reads `clamp(16384 / columns, 16, 512)` entries with `ReadFrame`, in runs of 32 neighbours spread over the list (`AutoIndex`), and measures every column of each entry (`MeasureCell`).
- `ApplyAuto` skips columns in `_setByHand` (public `SetColumnWidth`, which a drag and a double-click fit use). A table with nothing open inside takes all widths at once (`ApplyAllAtOnce`: one snapshot, one `ApplySnapshots`); otherwise `ApplyOneByOne`.
- Off by default in `ViewerSession`; on in `ViewerModel` (`AutoFitColumns { init; }`).

## Measured

Box (Linux, 8 cores), optimized build, warm cache, `/workspace/bench3 auto` (`AUTO=0` off, `AUTO=1` on): open, expand the first list, tick until the fit is done, then 50 × (scroll 10 rows + tick).

| File | Load off / on | Fit after expand | Longest tick | Scroll + tick, avg off / on |
| --- | --- | --- | --- | --- |
| r50000.xml | 180 / 173 ms | 36 ms over 5 ticks | 8 ms | 1.24 / 1.24 ms |
| r300000.xml | 585 / 568 ms | 39 ms over 5 ticks | 10 ms | 1.47 / 1.41 ms |
| r1000000.xml | 825 / 841 ms | 39 ms over 5 ticks | 8 ms | 1.37 / 1.48 ms |
| r1000000.json | 799 / 737 ms | 28 ms over 4 ticks | 8 ms | 0.98 / 0.97 ms |
| r3000000.xml | 1864 / 1487 ms | 36 ms over 4 ticks | 13 ms | 1.22 / 1.31 ms |
| r3000000.json | 1487 / 1252 ms | 27 ms over 4 ticks | 10 ms | 0.83 / 0.83 ms |
| z1000000.xml (Chinese) | 962 / 836 ms | 39 ms over 5 ticks | 9 ms | 1.36 / 1.45 ms |
| w200.xml (200 columns) | 600 / 597 ms | 93 ms over 9 ticks | 33 ms | 19.4 / 21.7 ms |
| w1000.xml (1000 columns) | 493 / 488 ms | 97 ms over 3 ticks | 81 ms | 49.9 / 45.3 ms |
| w1000.json (1000 columns) | 8257 / 8392 ms | 115 ms over 3 ticks | 102 ms | 44.9 / 37.0 ms |

Load differences are noise (the fit does not run during the scan). The longest tick on wide tables is mostly the redraw after the widths change, which costs the same as one scroll frame of that table (about 20-50 ms).

## Follow-up

- Record tables (name and value columns of a record) are not fitted on their own.
- A redraw of a table with hundreds of columns lays out every column of each visible row, not only the visible ones; that is the 20-50 ms scroll frame above.
