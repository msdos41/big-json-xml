# 01: Find what matches values and still reveals

**What to build:** A person inspecting a document gets a find window and a results window instead of a toolbar query and a docked hit list. Find what is a character sequence. An empty Find what produces no hits. With the defaults, the search looks in values, the value contains Find what, and Match case is on. Hits appear in document order while the scan is still running. Each result shows the field text, with the matched stretch marked, and the path in slash form. A short value is shown whole. A long value is a window around the match, as wide as the results column. Choosing a row expands along that path and scrolls the one document surface to that cell, which is marked as a whole cell.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The toolbar query and the docked hit list are gone. The find window and the results window stay open over the document surface.
- [x] Find what searches attribute values and character data. Element names and attribute names are not tested yet.
- [x] The match is contains. Match case is on. An empty Find what has no hits. `*` is an ordinary character.
- [x] `=` and `~` are not operators. A collapsed list label such as `Name[n]` is not a hit.
- [x] Hits stream in document order, including from tables that are not expanded.
- [x] A result row shows the field text and a Path such as `/Document/List[6]/Field/#text`. The matched stretch is marked in the text. A long value shows only the window around the match that fits the column.
- [x] Choosing a row reveals that hit: the embedded tables on the path open, the one document surface scrolls to the cell, and the whole cell is marked.
- [x] Session tests cover the value match, the empty sequence, streaming order, the stretch, and reveal. Window-model tests cover the two windows and choosing a row.
