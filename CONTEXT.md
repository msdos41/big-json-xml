# BigJsonXml

A desktop viewer that presents a large XML document as record tables and list tables so a person can scroll and search without treating the document as a tree of objects. JSON is out of scope for the current product.

## Language

**Document**:
An XML file opened for inspection. Comments and processing instructions are not presented. The product does not change the document.
_Avoid_: File (when you mean the opened XML), payload, dump, editor buffer

**Viewer**:
A read-only session over a document: open, present tables, scroll, search. There is no edit and no save.
_Avoid_: Editor

**Record table**:
A table of one element's fields, with the field names as row headers (vertical headers). This is the presentation when the element is not one entry among same-named siblings.
_Avoid_: Form, property grid, vertical table, key-value sheet

**List table**:
A table of two or more sibling elements that share a parent and a name, whether or not they are consecutive. Field names are column headers (horizontal headers). One entry per element. A single occurrence of a name is not a list table.
_Avoid_: Grid, sheet, DataGrid, horizontal table

**Entry**:
One element in a list table.
_Avoid_: Row, record, item, node

**Field**:
One attribute, one text value, or one child name of an element. On a record table a field is a row. On a list table a scalar field is a column.
_Avoid_: Property, key, column (that word only describes a list table), row (overloaded)

**List field**:
A field whose child name occurs two or more times under the element. It is presented as a list table, not as a scalar cell.
_Avoid_: Array column, nested column, sub-table column

**Scalar field**:
An attribute, the element's own character data, or a child that occurs once and has only character data. An attribute's name is written with a leading `@`.
_Avoid_: Value, cell

**#text**:
The field that holds an element's own character data when that element also has attributes or element children.
_Avoid_: Inner text, value

**Embedded table**:
A record table or list table drawn inside the cell that was expanded. The row that owns that cell grows with the embedded table. It is not another screen, pane, or window.
_Avoid_: Sub-table, drill-down, popup, nested view
