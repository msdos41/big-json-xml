# Virtualize Find All results

Status: resolved

## Answer

Replaced progressive `ObservableCollection` dump with `VirtualResultLines` + `FindResultList`. Hit count publishes immediately; `ReadHitText` only on indexer access. ADR 0056.
