# 01: Lock the outline and the hits

**What to build:** Nothing gets faster yet. A person inspecting a document gets the same outline and the same hits as today, and that result is pinned by the viewer session so later speed work cannot drift. The pinned documents are not the example files. They are generated documents the viewer already accepts: nested XML with UTF-8 text, a namespace, an internal entity, and the same name repeated at more than one depth, plus nested JSON with objects, arrays, and scalar literals.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The viewer session is the only seam. Tests open a document, pump the scan, search, and read the outline and the hits.
- [x] For the generated XML document, the outline's names, kinds, list counts, and columns match what the session reports today.
- [x] Opening one entry uses the place the scan stored. Collapsing the list does not drop that place.
- [x] A text search reports the same hits as today for every condition: contains, starts with, entire cell, Match case on and off, element name, attribute name, and value each switched off, plus a field name, a parent element name, a grandfather element name, and a parent attribute name.
- [x] Each hit keeps its path, match offset, and match length. Two matches in one field stay two hits. Hits stay in document order.
- [x] An empty Find what has no hits. Comments are not hits. A JSON attribute name and a JSON parent attribute match nothing.
- [x] A JSON value is the literal the cell shows. `{}` and a collapsed `Name[n]` are not values.
- [x] A repeated JSON key still stops the scan. Empty text, whitespace only, or a second JSON value still does not open.
- [x] This ticket does not change how long a scan or a search takes, and it does not add a seconds target.
