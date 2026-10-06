# 01: Open a JSON object as a record table

**What to build:** A person can open a JSON document whose value is an object and read it as a record table in the same Viewer as XML. Keys are fields in file order. Scalars are shown as JSON literals. A nested object collapses to `{}` and expands inside the cell. The first byte chooses the grammar. The open dialog can filter JSON files, and the suffix still does not choose.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Bytes whose first value byte is `{` open as a JSON document. A leading BOM and whitespace are ignored and are not fields.
- [x] A root object is a record table. Keys appear in the order they occur in the file. Each key is one field, named with the decoded key and no leading `@`.
- [x] A string is shown in double quotes, decoded, with `"` and `\` written as `\"` and `\\`. A number, `true`, `false`, and `null` are shown as in the file, without quotes. An empty string is `""`. `1.0`, `1e2`, and `-0` are not rewritten. `\n` is a newline, `\u0041` is `A`, and `\/` is `/`.
- [x] `"null"`, `"1"`, and `"true"` stay distinct from `null`, `1`, and `true`.
- [x] `{}` is a record table with no fields. A collapsed JSON object shows `{}` whether or not it has keys. Expanding it draws the record table inside the cell.
- [x] Whitespace between tokens is not a field.
- [x] A presentation is available before the scan finishes. Scan fraction and remaining time advance as they do for an XML document. A long string is a width-limited window, its full value is the same literal, and only the visible slice is decoded.
- [x] A first byte `<` still opens as XML, including when the file name ends in `.json`. A first byte that starts neither grammar, an empty file, and whitespace only do not open.
- [x] The open dialog offers a JSON filter. The file name is not what chooses the grammar.
- [x] The session does not write the document and does not return it as a tree or as one string.
- [x] Viewer session tests cover the object, the literals, the nested object, the BOM, the XML first byte, and the refusals.
