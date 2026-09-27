# 01: Open a document as a record table

**What to build:** A person can open an XML document and see the document element as a record table: field names down the side, attribute values and simple child text in the cells. The window shows that presentation. The behavior is tested through the Viewer session.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] Opening a document shows the document element as a record table, not as a one-row list table.
- [ ] An attribute is a field named with a leading `@` and shows its expanded value.
- [ ] A child that occurs once and has only character data shows that text and has no expand control.
- [ ] When an element has attributes or element children and also has non-whitespace text, that text is a `#text` field. Several such pieces become one value, concatenated in document order and placed at the first piece.
- [ ] Whitespace-only text is not a field, including under `xml:space="preserve"`. Spaces beside real text are kept.
- [ ] Comments and processing instructions do not appear.
- [ ] An empty element still appears, with an empty scalar value.
- [ ] Internal entities are expanded in the text that is shown.
- [ ] A presentation is available before the scan reaches the end of the file.
- [ ] The session does not return the document as a tree or as one string.
- [ ] Viewer session tests cover the behavior above, and the window shows the same presentation.
