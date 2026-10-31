# Find All result list yields to the UI

Status: ready-for-human

## Problem

On 0.1.1, Find All described hits as the scan/search pump found them, so the results list grew across ticks and the scrollbar stayed usable. After ADR 0053, a background search can hand over a large batch of hits in one `Pump`. `SyncResults` then called `ReadHitText` and appended a `FindResult` / `ResultHitLine` for every hit on the UI thread in one `Refresh`, freezing the window. Esc could not run until that finishes, and Esc previously only cleared the mark.

## Solution

- Describe at most a small budget of new hits per sync (wall-clock slice), then return so the next tick continues.
- Keep listing until every session hit has a results row (no hard row cap).
- Esc cancels the search thread and stops further listing for the current search.
- Find Next stays on the listed hits and does not wait for the whole list to materialize.

## Out of scope

- Making XmlSkipList cover Capital `mgr` sibling roots (BD_.xml).
- Virtualizing beyond Avalonia's ListBox defaults.
