# 02: Treat carriage return as whitespace

**What to build:** Indentation and blank gaps between elements are not fields when they contain only spaces, tabs, carriage returns, and line feeds. A pretty-printed document with Windows line endings does not grow a blank `#text` row between fields. Real text keeps the whitespace beside it.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] A gap that is only a carriage return, only a line feed, or a CRLF pair, with or without spaces and tabs, is not a field.
- [ ] `xml:space="preserve"` does not turn those gaps into `#text`.
- [ ] A value that contains a non-whitespace character keeps the spaces and line breaks written beside that character.
- [ ] When an element has other fields and its own non-whitespace text, that text is still `#text`.
- [ ] The checks use small documents on the Viewer session, including a lone carriage return and a CRLF gap.
