# Budget SyncResults / SyncVisible

Status: resolved

Describe a bounded number of new hits per sync so Tick stays interactive with tens of thousands of hits already in `Hits`.

## Answer

`DescribeMore` lists at most `ResultDescribeBudget` rows per sync under a wall-clock deadline, caps the list at `MaxListedResults`, and `CancelFind` freezes `ListStop`. Esc on the find, results, and main windows calls it. ADR 0056.
