# Do not turn a list table into a tree

On a list table, a repeating or nested child is not an expander that inserts child rows into the parent grid. Row expanders were rejected because they turn entries into a tree. In-row embedded grids were rejected because row height becomes unbounded.

ADR-0008 replaced the assumption that every parent is a horizontal table. A record table uses vertical field headers. Whether a list field on a record table opens by drill-in or by expanding in place is not decided here.
