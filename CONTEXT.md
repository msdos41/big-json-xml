# BigJsonXml

A desktop viewer that presents a large XML or JSON document as record tables and list tables so a person can scroll and search without treating the document as a tree of objects.

## Language

**Document**:
An XML file or a JSON file opened for inspection. The product does not change the document. Comments and processing instructions in an XML document are not presented.
_Avoid_: File (when you mean the opened document), payload, dump, editor buffer

**Scan**:
The one forward reading of a document, during which the viewer is scanning. It leaves an outline, and it ends when the end of the document is reached.
_Avoid_: Index, import, load, parse

**Scan fraction**:
How far the scan has read, as the bytes already reached divided by the document's length. It exists only while that length is fixed and greater than zero.
_Avoid_: Progress, percent complete, load progress

**Remaining time**:
The wall-clock time the scan is expected to need for the bytes still ahead, using how fast it has read since it started.
_Avoid_: ETA, countdown, time left

**Outline**:
What the scan keeps of an element: each field's name and kind, and for a list field its count, columns, and enough to open any entry without reading the other entries. A closed record has an outline and no second copy of that element.
_Avoid_: DOM, tree, cache, body

**Viewer**:
A read-only session over a document: open, present tables, scroll, search. There is no edit and no save.
_Avoid_: Editor

**Record table**:
A table of one element's fields, with the field names as row headers (vertical headers). This is the presentation when the element is not one entry among same-named siblings. Field names share one width. Values share another. Making the values wider widens every value cell and does not expand those fields.
_Avoid_: Form, property grid, vertical table, key-value sheet

**List table**:
A table of two or more sibling elements that share a parent and a name, whether or not they are consecutive. Field names are column headers (horizontal headers). One entry per element. A single occurrence of a name is not a list table. Columns stand in the order the scan first sees each field. A field seen later is added at the end.
_Avoid_: Grid, sheet, DataGrid, horizontal table

**Entry**:
One element in a list table. Opening it uses what the scan already learned, including after the list is collapsed, and does not read the other entries again.
_Avoid_: Row, record, item, node

**Field**:
One attribute, one text value, or one child name of an element. On a record table a field is a row. On a list table a scalar field is a column. An attribute name is written with a leading `@`. A name in a start tag with no value is still an attribute, and that value is empty. A child element is a field once the scan has seen that it has an attribute or an element child, even while its end tag is still ahead. A name from a namespace is shown with the prefix written in the file; its identity is the namespace URI plus the local name.
_Avoid_: Property, key, column (that word only describes a list table), row (overloaded)

**Cell**:
The rectangle in which one field is shown. On a record table it is the value beside a field name. On a list table it is where one entry meets one column. Expanding and collapsing that field happen inside this rectangle. Doing so does not expand or collapse a sibling cell.
_Avoid_: Box, pane

**List field**:
A field whose child name occurs two or more times under the element. Expanded, it is a list table inside the cell. Collapsed, the cell reads `Name[n]`, where `n` is how many siblings of that name have been seen. The count comes from the scan that is already walking those siblings; it is not a separate pass.
_Avoid_: Array column, nested column, sub-table column

**Scalar field**:
An attribute, the element's own character data, or a child that occurs once and has only character data. An attribute's name is written with a leading `@`. A collapsed cell shows one line: the start of the value, or a window around a hit, for as much of the value as the cell's width holds, up to the whole value.
_Avoid_: Value, cell

**Full value**:
The complete character data of a scalar field, shown inside the cell after the user expands it. The text wraps to the cell's width, and the row grows to the virtual height of that text on the document's one scroll surface. Only the visible slice is decoded. Collapsing the cell returns it to the one-line, width-limited view.
_Avoid_: Text viewer, popup, inner scroll

**#text**:
The field that holds an element's own character data when that element also has attributes or element children, and that data contains a character other than space, tab, carriage return, or line feed. Text made only of those characters is not a field, including under `xml:space="preserve"`.
_Avoid_: Inner text, value, indent

**Embedded table**:
A record table or list table drawn in the cell that was expanded, bounded by that cell's rectangle. It is not laid out as rows under the entry across the rest of the table. Sibling cells keep their own rectangles. The entry's row is as tall as its tallest cell. The document has one vertical scrollbar and one horizontal scrollbar. An embedded table does not add its own. It is not another screen, pane, or window.
_Avoid_: Sub-table, drill-down, popup, nested view

**Find what**:
The character sequence a text search looks for.
_Avoid_: Query, query string, pattern

**Text search**:
A search for Find what in element names, attribute names, and values over the whole document, including tables that are not expanded, with each of those three able to be turned off. The tested text contains Find what, starts with it, or equals it; comparison is ordinal and case-sensitive unless Match case is turned off; an empty Find what has no hits, and comments are not searched.
_Avoid_: XPath, full-text index, grep, query string, wildcard

**Field name**:
The name of the field a hit is on, written as the table shows it, including `@`, `#text`, and a prefix from the file. A text search may require this name. An empty requirement allows every field.
_Avoid_: Parent element name, query string, XPath

**Field search**:
A text search limited by a field name. The value or the name still follows that text search's contains, starts-with, or equals rule.
_Avoid_: Column filter, XPath, predicate, query string

**Parent element name**:
The name of the element that directly contains the matched field. A text search may require this name. The name has no list index. A hit with no such element does not qualify.
_Avoid_: Field name, ancestor, XPath

**Grandfather element name**:
The name of the element that directly contains the parent element of the matched field. A text search may require this one step and no step further up. A hit with no element at that step does not qualify.
_Avoid_: Ancestor, XPath

**Parent attribute name**:
An attribute the parent element has, written as the table shows it, including `@`. A text search may require that the parent has this attribute. The attribute's value is not compared.
_Avoid_: Field name, attribute value

**Path**:
The steps from the document element down to a hit or an entry, written with a leading `/` and the steps joined by `/`. Each step is a field name, a list step is `Name[n]` for the entry's position among siblings of that same name, and a text step is `#text`.
_Avoid_: XPath, byte offset, row number, `text()`

**Hit**:
One search match. It remembers its path and the stretch of that field's text that matched, so the viewer can expand the embedded tables on that path and scroll that cell into view; two matches in one field are two hits, and a hit on a list entry's own element name is that entry.
_Avoid_: Result row, match span
