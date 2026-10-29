# Case-insensitive search jumps between candidates

Status: ready-for-human

## Problem

After PR #13, a search with Match case off still parsed every entry: about 17 s on x1g.xml, 14 s on j1g.json, 30 s on d1g.xml and 41 s on the 512 MB scalar array. The window stayed responsive, but the results took that long.

## Solution

- `CandidatePattern` gains `Fold`. A folded pattern is searched with `CandidateFinder.IndexOfFolded`: compare the first and last bytes 16 at a time (letters with the 0x20 bit set), then check the full pattern at each place both match. A pattern with no letters uses the plain `IndexOf`.
- `PlanSkip` no longer requires Match case. The needle and its split-by-tag variants are folded. `&`, `xmlns`, `<![CDATA[` and `\` stay exact. The `#value` check ignores case when the search does.
- JSON jumps no longer need the item reader, so Starts with, Entire cell and name filters jump too.
- Why it is safe: under `OrdinalIgnoreCase` no character above U+007F equals an ASCII character (checked for every BMP character), so a case-insensitive hit's raw bytes hold the needle with only its letters' case changed. Hits are still found and checked by the same matcher.
- ADR 0054 amends ADR 0053.

## Measured

Box (Linux, 8 cores), Release, `/workspace/bench2` (`run-ci.sh`, `run.sh`), warm cache, second run. The `-ci` files are copies of x1g/j1g/d1g with `QZXJW`/`QZXJV` (`QZX`/`QZV` in d1g) written over text in the middle and last entries. t512m has only digits, so its needles are 7, 0 and x.

| File, case ignored | Middle hit | End hit | No hit |
| --- | --- | --- | --- |
| x1g-ci.xml | 16.9 s -> 0.50 s | 16.6 s -> 0.49 s | 16.8 s -> 0.40 s |
| j1g-ci.json | 14.2 s -> 0.24 s | 14.3 s -> 0.20 s | 14.3 s -> 0.20 s |
| d1g-ci.xml | 30.1 s -> 0.36 s | 31.5 s -> 0.37 s | 30.0 s -> 0.35 s |
| t512m.json | 41.2 s -> 0.10 s | 41.7 s -> 0.10 s | 41.2 s -> 0.12 s |

Case-sensitive (`run.sh`, second run) is unchanged: x1g 0.53/0.55/0.39 -> 0.52/0.54/0.36 s, j1g 0.22/0.21/0.22 -> 0.22/0.21/0.22 s, d1g 0.38/0.42/0.26 -> 0.37/0.43/0.26 s, t512m 0.10/0.12/0.13 -> 0.10/0.09/0.10 s. A needle with common letters at both ends (`eqe`) costs 0.21 s on j1g and 0.39 s on x1g. Peak RSS drops from about 135-160 MB to 53-77 MB, the same as case-sensitive search.

## Follow-up

- Needles with spaces or non-ASCII characters still parse every entry.
- XML lists below the root, and roots with more than one list, still parse every entry.
- Searches started while the scan runs stay on the pump.
