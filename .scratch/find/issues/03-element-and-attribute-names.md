# 03: Element names and attribute names

**What to build:** A person can include or exclude element names, attribute names, and values. All three start included. A name is the spelling shown in the table. A hit on a name shows that name, and revealing it lands on that field's row or, for a list entry's own name, on that entry's row.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** ready-for-agent

- [ ] Element name, attribute name, and value are on by default, and each can be turned off.
- [ ] An attribute name includes `@`, so Find what `id` matches `@id`, and Entire cell with Find what `@id` matches that whole name.
- [ ] A namespaced name is typed with the prefix written in the file. The field's identity remains the namespace URI plus the local name.
- [ ] When a name and a value both match, they are two hits.
- [ ] A name hit shows the field name in the results, with the matched stretch marked.
- [ ] Revealing a record-table field name lands on that field's row. Revealing a list entry's own element name lands on that entry's row.
- [ ] Comments and processing instructions are not hits.
- [ ] Session tests cover the three targets, `@`, a prefix, and a list-entry name. Window-model tests cover turning a target off and revealing a name hit.
