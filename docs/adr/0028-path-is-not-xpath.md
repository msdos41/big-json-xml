# A path is written with slashes and #text

A hit's path is shown as `/Document/List[6]/Start_node/#text`. An attribute step keeps its `@`. This is the Path the hit already stores, joined by `/`. It is not an XPath expression. The column is not named XPath, a text step stays `#text`, and the string is not a query.

The reference find-all list uses `text()` and the heading XPath. That spelling was rejected because a reader would treat the line as executable XPath and then build the element index ADR-0013 refused. The spaced form `A / B[6] / #text` was the previous display. The slash form keeps that same Path and reads as a hierarchy.