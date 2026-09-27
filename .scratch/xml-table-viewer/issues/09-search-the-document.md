# 09: Search the whole document

**What to build:** A person can search the whole document without expanding tables. A plain query finds text in attribute values and character data. `name = value` matches a whole field value, and `name ~ value` matches a value that contains the sequence. Hits appear while the scan is still running.

**Blocked by:** 03: Show list fields and live counts; 08: Show namespaced fields

**Status:** resolved

- [ ] A query with no `=` and no `~` is a case-sensitive text search over attribute values and character data, not over element or attribute names.
- [ ] The first `=` or `~` in the query is the operator. `=` matches the whole expanded value. `~` matches when the value contains the sequence. Both the field name and the value are case-sensitive.
- [ ] The field name is the trimmed text before the operator and uses the displayed spelling, including `@` and prefixes. A prefixed name matches namespace URI plus local name. An unprefixed element name matches no namespace, or the default namespace in scope at that element.
- [ ] The value is the text after the operator with only the whitespace immediately after the operator removed. Trailing spaces in the value are significant.
- [ ] Hits are reported as they are found, in document order, including for lists that are not expanded.
- [ ] Text search does not match a collapsed label such as `Order[1000000]`. Field search does not treat a list field as a scalar value.
- [ ] A hit stores a path from the document element and, when the match is inside a scalar, the character offset of the match in the expanded value. A list step carries the 1-based position among siblings of that same name.
- [ ] Viewer session tests cover text search, both operators, case, prefixes, a collapsed list, and streaming order, and the window lists hits as they arrive.
