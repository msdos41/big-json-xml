# A scalar cell shows a window, not the whole value

A scalar cell shows at most 200 characters: the start of the value, or a window around a search hit that landed inside it. The viewer does not decode a multi-megabyte text node into one string to paint the cell. The complete value must still be reachable. Whether that happens by growing the row on the outer scroll surface, or by another surface, is not decided.
