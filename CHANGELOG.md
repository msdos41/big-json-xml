# Changelog

## [0.1.1] - 2026-10-28

### Fixes

- Find Next no longer fails when a match is found while the file is still loading and the next sibling element has only been partly read.

## [0.1.0] - 2026-10-28

First public release: a Windows viewer for large XML and JSON files.

### Features

- Open an XML or JSON file read-only and view it as nested tables. The format is detected from the first byte after an optional BOM and whitespace.
- Repeated sibling elements and JSON arrays open as list tables. A single element or object opens as a record table. Attributes keep their `@` prefix.
- Expand a cell to show its nested table in place. Long values wrap when the cell is expanded. The whole document shares one vertical and one horizontal scrollbar.
- Search names and values, including inside collapsed tables. Find Next steps through hits, Find All lists them, and Esc clears the highlight.
- Browse and search while a large file is still loading. Progress shows bytes read, with an estimated time remaining.
- Large files are read from disk on demand instead of being loaded whole, so memory use stays low.
- Ctrl+O opens a file and Ctrl+F opens Find. Double-click a column border to fit the column.
