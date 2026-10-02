# A path is written with slashes and #text

A hit's path is shown as `/Document/List[6]/Start_node/#text`. An attribute step keeps its `@`. This is the Path the hit already stores, joined by `/`. It is not an XPath expression. A text step stays `#text`, and the string is not a query.

The results table heads that column XPath, the same heading as the find-all reference. The heading does not make the cell an expression. The reference spells a text step `text()`. That spelling stays rejected: a reader would paste the line as a query and then expect the element index ADR-0013 refused. The slash form keeps the Path and reads as a hierarchy.