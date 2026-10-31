# Budget SyncResults / SyncVisible

Status: resolved

Describe a bounded number of new hits per sync so Tick stays interactive with tens of thousands of hits already in `Hits`. Every hit is still listed; there is no hard row cap.

## Answer

`DescribeMore` lists at most `ResultDescribeBudget` rows per sync under a wall-clock deadline. `CancelFind` freezes `ListStop`. Esc on the find, results, and main windows calls it. A hard Visible/list cap was rejected. ADR 0056.
