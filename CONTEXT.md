# BigJsonXml

A desktop viewer that presents large XML documents as nested tables of like elements so a person can scroll and search without treating the document as a tree of objects. JSON is out of scope for the current product.

## Language

**Document**:
An XML file opened for inspection. The product does not change the document.
_Avoid_: File (when you mean the opened XML), payload, dump, editor buffer

**Viewer**:
A read-only session over a document: open, present tables, scroll, search. There is no edit and no save.
_Avoid_: Editor

**Table**:
A two-dimensional view of sibling elements treated as the same kind of record: one row per element, one column per field. The rule that decides which siblings belong together is not yet fixed.
_Avoid_: Grid, sheet, DataGrid, TreeDataGrid

**Row**:
One element instance in a table.
_Avoid_: Record, node (when you mean a table row), item

**Column**:
A named field of a table, taken from the row element's attributes or simple children.
_Avoid_: Property, key, cell (a cell is a value at a row/column)

**Sub-table**:
A table of nested elements reached from a parent table. How the user opens it is not yet fixed.
_Avoid_: Nested grid, child grid, drill-down, expander (UI control names)
