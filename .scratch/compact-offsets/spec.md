# Compact list offsets

Status: ready-for-human

## Problem

The managed heap grew by at least eight bytes for every list entry, and doubled while a list's array grew. A 512 MB XML file of 67 million tiny entries peaked at about 1 GB. A 512 MB JSON array of 268 million scalars peaked at 6 GB, scanned at 13 MB/s, and expanding it took 3.7 s because the offsets were copied.

## Solution

- `EntryOffsets` packs offsets per block of 256 (ADR 0052). XML and JSON lists use it. A JSON list's slot and its opened table share one store.
- Later plain scalar items of a JSON array are taken in a tight run that writes their starts directly (`JsonParser.TakeScalarRun`). Anything else falls back to the item reader.

## Measured

Box (Linux, 8 cores), Release, `/workspace/bench` scan mode, one run each. Heap is the managed heap after the scan; peak RSS is the process high-water mark.

| File | Before | After |
| --- | --- | --- |
| t512.xml (67M `<a>1</a>`) | 42 MB/s, heap 513 MB (peak 769 MB), peak RSS 1051 MB | 76-82 MB/s, heap 9 MB, peak RSS 52 MB |
| t512.json (268M scalars) | 13 MB/s, heap 4097 MB (peak 6145 MB), peak RSS 6169 MB | 142-162 MB/s, heap 33 MB, peak RSS 71 MB |
| x1g.xml (records) | 361 MB/s, peak RSS 140 MB | 439 MB/s, peak RSS 51 MB |
| j1g.json (records) | 339 MB/s, peak RSS 220 MB | 406 MB/s, peak RSS 50 MB |
| d1g.xml (deep, small lists) | 338 MB/s, peak RSS 73 MB | 374 MB/s, peak RSS 47 MB |

## Follow-up

- Memory-mapped reads (`MemoryMappedFile`): measure whether `StreamByteSource` reads cost anything before changing it.
- Text search over a scalar array still runs at about 20 MB/s. That belongs to the background search step.
