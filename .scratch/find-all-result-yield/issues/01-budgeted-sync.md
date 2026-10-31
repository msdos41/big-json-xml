# Virtualize Find All results

Status: resolved

## Answer

`VirtualResultLines` + `FindResultList.Placeholder` never call `ReadHitText` on the scroll path. Bodies fill on the UI timer via `PumpDescribe` with a prefetch window. ADR 0056.
