# 04: Halve repeated micro-lists with cross-parent shape prediction

**What to build:** The XML parser retains verified list element shapes across parent element closures, keyed by `(ParentName, ChildName)`. When a subsequent parent opens and encounters that child element name, entry 1 speculatively attempts fast flat parsing. If speculative validation fails due to CDATA, comments, entities, or structural changes, the parser immediately falls back to safe standard reading and marks the key ineligible for speculation. The cache is bounded to 512 entries with a FIFO or LRU eviction policy. The two-entry cold start is eliminated for subsequent identical micro-lists across the document.

**Blocked by:** 03: Eliminate hierarchical stack crawls and redundant column allocations in DocumentTree.

**Status:** resolved

- [x] `XmlParser` maintains a bounded cache (at most 512 entries) of learned element shapes keyed by `(ParentName, ChildName)`.
- [x] Subsequent occurrences of a cached `(ParentName, ChildName)` combination attempt flat scanning on entry 1 without waiting for standard promotion.
- [x] If speculative flat parsing fails, parsing seamlessly falls back to standard markup reading without losing position or failing the scan.
- [x] A failed speculative attempt blacklists that `(ParentName, ChildName)` key from future speculation for that session.
- [x] The cache is released upon session disposal, and peak working set does not exceed 1.5 times the baseline.
- [x] The number of full `Start` events emitted during the scan of `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` drops by more than 200,000.
