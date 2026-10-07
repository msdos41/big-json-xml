# 03: Finish a JSON scan sooner

**What to build:** A person inspecting a JSON document waits less for the scan. The same reading serves an array of objects, a nested array, a nested object, and scalar literals, at any depth. It does not key off the example documents' field names. When the scan ends, the outline is the one pinned in ticket 01, and the retained result is still that outline rather than a second body.

**Blocked by:** 01: Lock the outline and the hits.

**Status:** resolved

- [x] The viewer session scans a generated JSON document with nested objects and arrays. The outline matches ticket 01, including field order, list counts, and scalar literals.
- [x] Opening an entry uses the place the scan stored and does not read the other entries again.
- [x] One slice of the scan still returns well under a tenth of a second.
- [x] A text search of the generated document returns the hits pinned in ticket 01. A JSON value is still the literal the cell shows.
- [x] A repeated JSON key still stops the scan. Empty text, whitespace only, or a second value still does not open. Trailing bytes that are not another value still stop after the finished value.
- [x] After the scan, managed memory follows the outline, not a copy of the document. A collapsed object is still `{}`.
- [x] JSON stays its own grammar. This ticket does not change the XML scan.
- [x] The example documents are not fixtures and are not required for this ticket.
