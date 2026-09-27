# 03: Show list fields and live counts

**What to build:** Repeated sibling elements show up as a collapsed list field labeled `Name[n]`. A name that occurs only once stays on the record table. The count climbs while the document is still being scanned and does not require a second pass.

**Blocked by:** 01: Open a document as a record table

**Status:** resolved

- [ ] Two or more sibling elements with the same parent and the same name are one list field, not separate record fields and not a one-row list.
- [ ] A different-named sibling between them does not split the list and does not join it.
- [ ] A name seen once stays a field of the record table.
- [ ] When a second sibling of that name is seen, the field becomes a list field and does not turn back into a plain field.
- [ ] The collapsed cell reads `Name[n]`, where `n` is how many siblings of that name have been seen.
- [ ] Fed the document in chunks, `n` increases as further siblings are seen and is exact once that parent's children have all been seen.
- [ ] Viewer session tests cover promotion, non-consecutive siblings, and chunked counts, and the window shows `Name[n]`.
