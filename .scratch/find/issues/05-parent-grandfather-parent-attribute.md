# 05: Parent, grandfather, and parent attribute

**What to build:** A person can narrow a text search by the parent element name, the grandfather element name, and a parent attribute name. Each box is an exact displayed name, with no list index, and follows Match case. An empty box adds no limit.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** resolved

- [x] A simple `Start_node` whose text is `RTM=123` has parent element name `Extended_covering`, not `Start_node`.
- [x] `#text` under `Start_node` has parent element name `Start_node`.
- [x] Grandfather element name is exactly one step above that parent. A higher ancestor does not qualify.
- [x] A name matches without a list index, so `Extended_covering` covers every entry of that name.
- [x] A hit with no element at a required step is excluded.
- [x] Parent attribute name `@id` requires the parent element to have `@id`, and does not compare that attribute's value. `id` does not match `@id`.
- [x] All three follow the Match case switch. Empty boxes do not limit the search.
- [x] Session tests cover the simple parent, the `#text` parent, the grandfather step, a missing ancestor, and `@id`.
