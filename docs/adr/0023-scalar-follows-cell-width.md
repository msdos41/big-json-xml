# A scalar shows as much as the cell width allows

This supersedes the 200-character ceiling in ADR-0019. A collapsed scalar is one line. It shows the start of the value, or a window around a hit, for as many characters as the cell width holds. That includes the entire value when the width is enough. The default width is capped, so a long value starts cut off. Dragging the width wider reveals the continuation. There is no character ceiling.

Expanding the cell still shows the full value, wrapped to the cell width, with the row growing on the one scroll surface. Collapse returns to the one-line view. A separate text window and an inner scrollbar stay rejected.

A multi-megabyte value is not decoded into one string merely to paint a cell that shows a slice of it. Text outside the document viewport is not realized.
