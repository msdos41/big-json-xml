# 03: Show list fields and live counts

**What to build:** Repeated sibling elements show up as a collapsed list field labeled `Name[n]`. A name that occurs only once stays on the record table. The count climbs while the document is still being scanned and does not require a second pass.

**Blocked by:** 01: Open a document as a record table

**Status:** resolved

- [x] Two or more sibling elements with the same parent and the same name are one list field, not separate record fields and not a one-row list.
- [x] A different-named sibling between them does not split the list and does not join it.
- [x] A name seen once stays a field of the record table.
- [x] When a second sibling of that name is seen, the field becomes a list field and does not turn back into a plain field.
- [x] The collapsed cell reads `Name[n]`, where `n` is how many siblings of that name have been seen.
- [x] Fed the document in chunks, `n` increases as further siblings are seen and is exact once that parent's children have all been seen.
- [x] Viewer session tests cover promotion, non-consecutive siblings, and chunked counts, and the window shows `Name[n]`.
