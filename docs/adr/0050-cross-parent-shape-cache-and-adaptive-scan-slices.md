# Cross-parent shape cache and adaptive scan slices

A document with many small lists and deep nesting takes longer to scan than a single-list document of the same size. Mentor Graphics Capital XML documents expose this pattern: over eighty thousand separate list tables averaging about fourteen entries each, nested ten to fifteen levels deep. Ticket 03 halved the scan for repeated siblings inside one parent, but dropping the flat candidate at each parent close forces every subsequent list to pay the two-entry cold start again. Deep ancestor crawls and desktop time-slice idle intervals compound the wait.

This decision cuts scan time in half on deeply nested, multi-list documents while preserving the single-threaded reading and the existing outline.

## Decisions

1. **Direct Parent References and Announced Short-circuit**:
   - `Frame` holds a direct reference to its enclosing parent frame, eliminating linear stack traversals.
   - Upward recursion in `Announce` stops as soon as an ancestor is already announced as a non-simple record and no new field keys are introduced.
   - `Frame` tracks column changes through a dirty flag; `ColumnsOf` is not reallocated when no column has been added.
   - ADR 0021 remains satisfied: an open element is a field before its end tag.

2. **Cross-parent Shape Prediction**:
   - `XmlParser` retains verified list element shapes across parent closures, keyed by `(ParentName, ChildName)`.
   - When a subsequent parent opens and encounters that child, item 1 speculatively attempts `TryFlatElement`.
   - If speculative validation fails (e.g. comments, entity references, or mismatched child tags), the key is blacklisted for that document and parsing falls back immediately to the standard read.
   - The shape cache is bounded to 512 entries and released with the session, strictly respecting ADR 0049's working-set ceiling.

3. **SameShape Fast-path**:
   - When consecutive flat elements share the identical shape (`SameShape == true`), duplicate attribute tracking across `_attrSpans` and redundant string allocations are bypassed.

4. **Adaptive Desktop Scan Slicing**:
   - In `BigJsonXml.Desktop`, the scan slice increases from 32ms to 64ms (with a 4ms idle interval) when user input has been quiescent for at least 250 milliseconds.
   - Any pointer or keyboard interaction immediately drops the slice back to 32ms with a 16ms yield.
   - Background scan progress UI refreshes are throttled to thirty frames per second.
   - The scan remains single-threaded, and each slice returns well under one tenth of a second (ADR 0025, ADR 0049).
