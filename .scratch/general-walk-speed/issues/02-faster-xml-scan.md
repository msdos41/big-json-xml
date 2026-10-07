# 02: Finish an XML scan sooner

**What to build:** A person inspecting an XML document waits less for the scan. The same reading serves a repeated list, a list nested inside another list, a deep record, and a tree of distinct field names. It does not key off the example documents' element names or a fixed depth. UTF-8 text, a namespace, and an internal entity stay on that reading. When the scan ends, the outline is the one pinned in ticket 01, and the retained result is still that outline rather than a second body.

**Blocked by:** 01: Lock the outline and the hits.

**Status:** resolved

- [x] The viewer session scans a generated XML document with repetition at more than one depth, UTF-8 text, a namespace, and an internal entity. The outline matches ticket 01.
- [x] A tree of distinct field names uses that same reading. Its closed records keep an outline and no second body.
- [x] Opening an entry near the end, after the list is collapsed, does not read the other entries again.
- [x] One slice of the scan still returns well under a tenth of a second, so the window can take input while the scan runs.
- [x] A local document of about 500MB still finishes within fifteen seconds, both as repeated siblings and as distinct names. That bar is not tightened and the scan is not cut off at fifteen seconds.
- [x] A text search of the generated document returns the hits pinned in ticket 01.
- [x] After the scan, managed memory follows the outline, not a copy of the document. An external entity still stops the scan. Whitespace-only gaps are still not fields.
- [x] The example documents are not fixtures and are not required for this ticket.
