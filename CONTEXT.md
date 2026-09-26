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
A table of two or more sibling elements that share a parent and a name, whether or not they are consecutive. Field names are column headers (horizontal headers). One entry per element. A single occurrence of a name is not a list table. Columns stand in the order the scan first sees each field. A field seen later is added at the end.
_Avoid_: Grid, sheet, DataGrid, horizontal table

**Entry**:
One element in a list table.
_Avoid_: Row, record, item, node

**Field**:
One attribute, one text value, or one child name of an element. On a record table a field is a row. On a list table a scalar field is a column. An attribute name is written with a leading `@`. A name in a start tag with no value is still an attribute, and that value is empty. A child element is a field once the scan has seen that it has an attribute or an element child, even while its end tag is still ahead. A name from a namespace is shown with the prefix written in the file; its identity is the namespace URI plus the local name.
_Avoid_: Property, key, column (that word only describes a list table), row (overloaded)

**Cell**:
The rectangle in which one field is shown. On a record table it is the value beside a field name. On a list table it is where one entry meets one column. Expanding and collapsing that field happen inside this rectangle.
_Avoid_: Box, pane

**List field**:
A field whose child name occurs two or more times under the element. Expanded, it is a list table inside the cell. Collapsed, the cell reads `Name[n]`, where `n` is how many siblings of that name have been seen. The count comes from the scan that is already walking those siblings; it is not a separate pass.
_Avoid_: Array column, nested column, sub-table column

**Scalar field**:
An attribute, the element's own character data, or a child that occurs once and has only character data. An attribute's name is written with a leading `@`. The cell normally shows at most a 200-character window: the start of the value, or a window around a hit inside it. The value is not decoded into one string.
_Avoid_: Value, cell

**Full value**:
The complete character data of a scalar field, shown only after the user expands that cell. The row grows to the virtual height of the text on the document's one scroll surface. Only the visible slice is decoded. Collapsing the cell returns it to the 200-character window.
_Avoid_: Text viewer, popup, inner scroll

**#text**:
The field that holds an element's own character data when that element also has attributes or element children, and that data contains something other than whitespace. Whitespace-only text is not a field, including under `xml:space="preserve"`.
_Avoid_: Inner text, value, indent

**Embedded table**:
A record table or list table drawn in the cell that was expanded, bounded by that cell's rectangle. It is not laid out as rows under the entry across the rest of the table. Sibling cells keep their own rectangles. The entry's row is as tall as its tallest cell. The document has one vertical scrollbar and one horizontal scrollbar. An embedded table does not add its own. It is not another screen, pane, or window.
_Avoid_: Sub-table, drill-down, popup, nested view

**Text search**:
A case-sensitive search for a character sequence in attribute values and character data, written as a query with no `=` and no `~`.
_Avoid_: XPath, full-text index, grep

**Field search**:
A case-sensitive search for a named field over the whole document, including tables that are not expanded. `name = value` matches the whole value. `name ~ value` matches a value that contains the sequence.
_Avoid_: Column filter, XPath, predicate

**Path**:
The steps from the document element down to a hit or an entry. Each step is a field name and, for a list field, the entry's position among siblings of that same name.
_Avoid_: XPath, byte offset, row number

**Hit**:
One search match. It remembers its path so the viewer can expand the embedded tables on that path and scroll that cell into view.
_Avoid_: Result row, match span
