# 05: Parent, grandfather, and parent attribute

**What to build:** A person can narrow a text search by the parent element name, the grandfather element name, and a parent attribute name. Each box is an exact displayed name, with no list index, and follows Match case. An empty box adds no limit.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** ready-for-agent

- [ ] A simple `Start_node` whose text is `RTM=123` has parent element name `Extended_covering`, not `Start_node`.
- [ ] `#text` under `Start_node` has parent element name `Start_node`.
- [ ] Grandfather element name is exactly one step above that parent. A higher ancestor does not qualify.
- [ ] A name matches without a list index, so `Extended_covering` covers every entry of that name.
- [ ] A hit with no element at a required step is excluded.
- [ ] Parent attribute name `@id` requires the parent element to have `@id`, and does not compare that attribute's value. `id` does not match `@id`.
- [ ] All three follow the Match case switch. Empty boxes do not limit the search.
- [ ] Session tests cover the simple parent, the `#text` parent, the grandfather step, a missing ancestor, and `@id`.
