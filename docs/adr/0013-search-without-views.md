# Search is text or field, over the document, and a hit can be revealed

The query grammar below is retired by ADR-0027. Revealing a hit, and the rejection of XPath, still hold.

Search does not require tables to be expanded. A query with no `=` and no `~` is a case-sensitive text search. `name = value` is a case-sensitive exact field search. `name ~ value` is a case-sensitive "contains" field search. There is no XPath. A hit stores a path from the document element, not a dependency on tables already built. Revealing a hit expands the embedded tables on that path and scrolls the single outer surface to that cell.

XPath was rejected because it needs a navigable index of every element. The original prompt used "XPath" for this whole-file search.
