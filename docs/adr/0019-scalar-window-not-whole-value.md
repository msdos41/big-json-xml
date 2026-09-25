# A scalar cell shows a window until the user asks for the whole value

A scalar cell normally shows at most 200 characters: the start of the value, or a window around a search hit inside it. The viewer does not decode a multi-megabyte text node into one string to paint that cell.

The user can expand that same cell to the full value. The row then grows to the virtual height of the text and moves on the document's one scroll surface. Only the visible slice is decoded. Collapse returns the cell to the 200-character window. Opening a list does not expand full values by itself.

A separate text window was rejected. An inner scrollbar in the cell was rejected. Both contradict the single scroll surface.
