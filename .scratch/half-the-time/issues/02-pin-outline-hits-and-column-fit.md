# 02: Pin the outline, the hits, and the column fit

**What to build:** Nothing gets faster yet. A person inspecting a document gets the same outline, the same hits, and the same fitted width as today, and that result is pinned by the viewer session so later speed work cannot drift. The pinned documents are generated documents the viewer already accepts, not the timing set: nested XML with UTF-8 text, a namespace, an internal entity, and the same name repeated at more than one depth, plus nested JSON with objects, arrays, and scalar literals. A long list of short cells is included so a column fit has to read every entry.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The viewer session is the only seam. Tests open a document, pump the scan, search, expand, column-fit, and read the outline, the hits, and the width.
- [x] For the generated XML document, the outline's names, kinds, list counts, and columns match what the session reports today. A closed record keeps an outline and no second body.
- [x] Opening one entry uses the place the scan stored. Collapsing the list does not drop that place.
- [x] A text search reports the same hits as today for every condition: contains, starts with, entire cell, Match case on and off, element name, attribute name, and value each switched off, plus a field name, a parent element name, a grandfather element name, and a parent attribute name.
- [x] Each hit keeps its path, match offset, and match length. Two matches in one field stay two hits. Hits stay in document order. An empty Find what has no hits.
- [x] A JSON value is the literal the cell shows. `{}` and a collapsed `Name[n]` are not values. A JSON attribute name and a JSON parent attribute match nothing. A repeated JSON key still stops the scan.
- [x] A column fit of a long list of short cells applies the width produced by measuring every entry. It stops growing once a cell is already five hundred characters. The width matches what the session produces today.
- [x] One slice of a scan, and one slice of a column fit, still returns well under a tenth of a second.
- [x] This ticket does not change how long a scan, a search, or a gesture takes, and it does not add a seconds target.
