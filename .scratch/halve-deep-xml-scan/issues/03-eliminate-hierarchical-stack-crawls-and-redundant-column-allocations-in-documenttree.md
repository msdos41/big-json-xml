# 03: Eliminate hierarchical stack crawls and redundant column allocations in DocumentTree

**What to build:** The document tree eliminates linear stack walks and redundant column list cloning during element start processing. Each rented frame holds a direct reference to its immediate parent frame, replacing stack search loops. Upward recursion in announce short-circuits as soon as an ancestor is already announced as a non-simple record and has not introduced new column keys. Frame tracks column additions with a dirty flag, skipping column list re-allocation when no column has been added. The outline and field visibility pinned in ticket 02 remain unchanged, and `DocumentTree.Apply` wall-clock time drops by at least one second on the benchmark document.

**Blocked by:** 02: Pin the nested outline, entry places, and unclosed field visibility.

**Status:** resolved

- [x] `Frame` stores a direct reference to its enclosing parent frame upon rent/push, and clears it on recycle.
- [x] `Announce` stops upward propagation immediately when an ancestor is already announced and its columns are not dirty.
- [x] `Frame` maintains a `ColumnsDirty` flag, set only when an unseen field key is added to the frame.
- [x] `ColumnsOf` is bypassed when `ColumnsDirty` is false, avoiding repeated `List<Column>` allocations on static record tables.
- [x] All outline tests pinned in ticket 02 continue to pass without regression.
- [x] Processing time spent inside `DocumentTree.Apply` during the benchmark document scan is reduced by at least 1,000 milliseconds.
