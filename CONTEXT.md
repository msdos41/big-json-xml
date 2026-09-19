# BigJsonXml

A desktop viewer that presents large XML documents as nested tables of like elements so a person can scroll and search without treating the document as a tree of objects. JSON is out of scope for the current product.

## Language

**Document**:
An XML file opened for inspection. Comments and processing instructions are not presented. The product does not change the document.
_Avoid_: File (when you mean the opened XML), payload, dump, editor buffer

**Viewer**:
A read-only session over a document: open, present tables, scroll, search. There is no edit and no save.
_Avoid_: Editor

**Table**:
A two-dimensional view of sibling elements that share a parent and a name, whether or not those siblings are consecutive. One parent may own several tables, one per child name.
_Avoid_: Grid, sheet, DataGrid, TreeDataGrid

**Row**:
One element instance in a table.
_Avoid_: Record, node (when you mean a table row), item

**Column**:
A scalar field of a table, taken from an attribute or a simple child of the row element. A simple child is an element that is not repeating under that row and has no element children of its own. The cell is that child's character data.
_Avoid_: Property, key, cell (a cell is a value at a row/column)

**Nested column**:
A field of a table whose cells are not scalar. Activating it opens a sub-table of the nested elements for that name. Repeating children and children that have element children are nested columns, not columns.
_Avoid_: Expander, tree column, drill-down column

**Sub-table**:
A table opened from a nested column. It does not change the parent table's rows or columns.
_Avoid_: Nested grid, child grid, drill-down, expander, tree

**#text**:
The column that holds a row element's own character data when that element also has attributes or children (mixed or element-plus-text content).
_Avoid_: Inner text, value (when you mean this column)
