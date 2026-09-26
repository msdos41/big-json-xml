---
status: superseded by ADR-0024
---

# Offset indexes exist only for expanded lists

Scrolling an expanded list to an arbitrary entry needs that entry's file offset. v1 builds that offset list only when the cell is expanded, while scanning that parent's children, and may drop it when the cell collapses. Search does not use this index. A hit keeps a path; revealing the hit builds offset indexes for the lists on that path.

A full-document element index was rejected. It is the cost of XPath, which v1 does not have. Re-scanning from the parent on every scrollbar jump was rejected because a million-entry list would stall.
