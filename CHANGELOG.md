# Changelog

## 0.1.0

### Features

- Open one XML or JSON document and read it as tables. The file on disk stays as it is.
- Same-named siblings, and a JSON array, open as a list table. One element or one object opens as a record table. An attribute keeps the `@` written in the file.
- Expand a cell to draw the next table inside it. A long value wraps when the cell is opened. The document has one vertical scrollbar and one horizontal scrollbar.
- Search names and values, including inside a table that is not expanded. Find Next steps through hits, Find All lists them, and Esc clears the mark.
- Scroll and search while a large file is still being read. The bar is bytes reached over the length, and the remaining time follows the pace so far.
- The first byte after a leading BOM and whitespace chooses the format: `<` is XML, and a JSON start byte is JSON.
- A large file stays small in memory.
- Ctrl+O opens a file, Ctrl+F opens Find, and double-clicking a column edge fits that column.
