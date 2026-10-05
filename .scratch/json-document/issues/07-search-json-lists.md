# 07: Search JSON lists

**What to build:** A person can search inside JSON lists and `#value` fields. A field-name hit lands on each entry that has the field, not on the column header. Parent and grandfather names climb named fields and skip an object that is only a list entry.

**Blocked by:** 03: Name a non-object JSON value #value, 06: Search JSON objects

**Status:** ready-for-agent

- [ ] A value search finds literals inside list entries and inside `#value`, using the same literal rules as 06.
- [ ] A hit on a field name is that field on each entry that has it. An entry that lacks the key does not hit. The column header is not a separate hit.
- [ ] A missing key adds no value hit. A collapsed `Name[n]` is not a value.
- [ ] In `{"wrap": {"nums": [{"id": "a1"}]}}`, a hit on the value of `id` has parent element name `nums` and grandfather element name `wrap`. The list index is not part of either name.
- [ ] A hit on a root object's own field has no parent. Requiring a parent excludes it.
- [ ] A parent-attribute requirement matches nothing in a JSON document.
- [ ] Field name `#value` limits hits to that field. An empty Field name still allows every field.
- [ ] Viewer session tests cover a per-entry name hit, a sparse key, a `#value` literal, the parent and grandfather, a missing parent, and a parent attribute.
