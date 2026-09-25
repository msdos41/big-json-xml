# One scrollbar for the document, including expanded cells

An expanded cell grows its row to the virtual height of the embedded table. Only the slice inside the viewport is realized. The document has one scrollable surface, which may move vertically and horizontally. Embedded tables do not get their own scrollbars, and a cell is not capped at the viewport height. Collapse returns the row to a single line.

A fully realized million-row child was rejected. An inner scrollbar was rejected because nested cells would stack scrollbars.
