# Nested structure opens as a column-scoped sub-table, not a tree

A nested or repeating child is a nested column on the parent table. Activating it opens a separate table of those children. The parent table stays a regular 2-D grid. Row expanders and in-row embedded grids were rejected: the first turns records into a tree; the second makes row height unbounded and fights virtualization.
