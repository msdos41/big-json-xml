# 04: Finish a text search sooner

**What to build:** A person inspecting a document waits less for a text search, on both XML and JSON, including when the search starts while the scan is still running. Every condition returns the hits pinned in ticket 01. The search is still a forward reading. It does not build an index and it does not read list-entry offsets.

**Blocked by:** 02: Finish an XML scan sooner. 03: Finish a JSON scan sooner.

**Status:** resolved

- [x] The viewer session searches a generated XML document and a generated JSON document. Hits match ticket 01 for contains, starts with, entire cell, Match case on and off, and each of element name, attribute name, and value switched off.
- [x] A field name, a parent element name, a grandfather element name, and a parent attribute name still narrow hits. An empty name limit adds no limit.
- [x] Two matches in one field stay two hits, in document order. An empty Find what has no hits. Comments are not hits.
- [x] On JSON, attribute names and parent attributes match nothing. `{}` and a collapsed `Name[n]` are not values. A hit on a field name is that field.
- [x] Hits appear while the scan is still running, in document order.
- [x] Starting the search after the scan has finished returns those same hits. Revealing a hit still has the path and the matched stretch.
- [x] The search does not leave a resident index, and the managed memory it adds is the hit list.
- [x] No new seconds target is added. The example documents are not fixtures.
