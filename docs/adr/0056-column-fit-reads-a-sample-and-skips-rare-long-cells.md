# Column fit reads a sample and skips rare long cells

Double-clicking a list column's edge read every entry of the list (ADR 0049). On a list of a million rows that took seconds of 32 ms slices, and much longer when the cells held non-ASCII text, because each such cell went through the frame reader. One very long cell also made the whole column as wide as that cell, up to 500 characters.

This amends ADR 0049 for list columns. Record name and value fits are unchanged: they still read the whole record and stop at 500 characters.

1. A list column fits from at most 4,096 entries (`FitSampleEntries`). A shorter list is read in full. A longer one is read at evenly spaced entries, the first and the last included. The work no longer grows with the number of rows.
2. The longest twentieth of the sampled cells does not count. The column takes the widest of the rest, so a few long cells are cut off with an ellipsis instead of widening the column. In a list of fewer than twenty entries this is the longest cell. Empty and missing cells do not count.
3. No fitted list column is wider than 60 characters (`FitColumnCharacters`, 480 px). The header fits too, up to the same cap. An open table inside the column still widens it as before.
4. UTF-8 text in a cell is counted from its bytes, one character per sequence and two past U+FFFF, the same count the frame reader gives. A file in another encoding, an entity, or a nested value still takes the frame reader for that entry.
5. Sampled entries that lie more than 64 KB apart are read with a small read through a second file handle, not through the 1 MB read-ahead window.

The width can differ from a full read: a long value in an entry the sample skips, or in fewer than one cell in twenty, no longer widens the column. A cut-off cell still shows its full text when opened or copied, and dragging the edge still sets any width.
