# An embedded table stays in its cell and widens that cell

Opening a cell does not open or close a sibling cell. The embedded table is laid out inside that cell's rectangle, not as rows under the entry across the rest of the table. The entry is as tall as its tallest open cell. This sharpens ADR-0009. ADR-0015's collapse rule applies when no cell in the row remains open.

On open, a list column, or the value side of a record table, grows to the embedded table's default width. Field names on a record table share one width and the values share another, so a wider value side widens every value cell and expands none of them. If the user then narrows that width, the embedded table is clipped to the cell and does not grow the width again. Collapse restores the width from before the open.

A width belongs to that table alone and lasts for the session. Another table with the same field names does not share it. Reopening the document restores the default. A width cannot be dragged so narrow that its header disappears. The default is capped, so a long value or a wide embedded table does not start at its full width until an open or a drag makes it so.

A clipped embedded table does not gain its own scrollbar. Pinning list headers to the viewport was rejected for the same reason: the document already has its one vertical scrollbar and its one horizontal scrollbar.
