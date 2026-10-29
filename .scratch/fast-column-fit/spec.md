# Fast column fit on long lists

Status: ready-for-human

## What was slow

Double-click on a list column edge (`ViewerModel.FitWidth` → `ViewerSession.BeginFit`/`AdvanceFit`) read every entry of the list in 32 ms slices, so the width only changed after the last slice.

- ASCII cells: about 0.5 µs per entry. 1M rows ≈ 0.6-0.9 s of slices, 3M rows ≈ 1.4-1.7 s.
- Non-ASCII cells (Chinese text): every entry fell back to `ReadFrame`, about 6.5 µs per entry. 1M rows ≈ 6.5-7 s.
- Cells with an entity (`&amp;`) also fell back: 1M rows ≈ 6.6-7.1 s.
- Many columns was not the cause: refresh after a fit is 1-25 ms; the fit itself reads row bytes.

## Changes

- Sample: at most `FitSampleEntries` (4096) entries, evenly spaced, first and last included (`SampleIndex`).
- Rule (`ListWidth`): header vs. the sampled cells without their longest twentieth; capped at `FitColumnCharacters` (60 chars, 480 px). Header capped at the same. Empty or missing cells are ignored.
- `ByteScan` counts UTF-8 sequences (JSON strings, XML text and attributes when the document is UTF-8).
- `StreamByteSource` implements `ISparseReadSource`: a sampled read far from the window goes through a second handle (`RandomAccess.Read`), so the 1 MB FileStream buffer plus the 1 MB window are not refilled per sample. Used when samples are more than 64 KB apart.
- `ByteScan` loops are `AggressiveOptimization`: one fit runs only 4096 entries, too few for tiering, and the first double-click after start ran unoptimized code.
- `LastFitReads` / `LastFitSlowReads` (internal) for tests.

## Measured

Box (Linux, 8 cores), optimized build, warm cache. `/workspace/bench3` (`fit` = `ViewerModel.FitWidth` then `Tick` until the fit is done, refresh included; `sfit` = `ViewerSession.Fit` alone). Files from `/workspace/gen/rows.py` (8 columns) and `rowszh.py` (half the columns Chinese).

| File | Before | After |
| --- | --- | --- |
| r50000.xml / .json, `name` | 132-158 / 37-53 ms | 24-32 / 13-25 ms |
| r300000.xml / .json | 217-778 / 210-226 ms | 22-31 / 11-31 ms |
| r1000000.xml / .json | 582-888 / 221-400 ms | 21-39 / 12-19 ms |
| r3000000.xml / .json | 1375-1704 / 712-1000 ms | 22-31 / 13-21 ms |
| z300000.xml / .json, `city` (Chinese) | 1834-2605 / 1712-2053 ms | 20-29 / 13-22 ms |
| z1000000.xml / .json, `city` (Chinese) | 6685-7061 / 6421-6615 ms | 20-41 / 15-22 ms |
| e1000000.xml, `name` with `&amp;` | 6567-7119 ms | 108-126 ms (frame reader, 4 slices) |
| w200.xml (200 columns, 20k rows) | 138-621 ms | 85-170 ms |

Load and expand are untouched (same code); times above match within noise.

## Follow-up

- Wide XML rows (hundreds of columns) still scan each sampled entry to its end, to catch a repeated child.
- The frame-reader fallback (entities, other encodings) costs about 26 µs per sampled entry.
