# 02: Pin the nested outline, entry places, and unclosed field visibility

**What to build:** A test suite harness pins the exact outline, entry places, columns, list counts, and unclosed field visibility for deeply nested and multi-list XML documents before any parser or tree modifications are made. A test opening the benchmark document through the viewer session receives the exact pinned outline and column sets. An entry place survives a collapse, and opening an entry uses its stored offset without reading the rest of the list. An unclosed non-simple element remains visible as a field in its parent record table during the scan, preserving ADR 0021.

**Blocked by:** 01: Record the baseline for deeply nested multi-list documents.

**Status:** ready-for-agent

- [ ] A test fixture pins the root fields, kinds, list counts, and column header names for the target document.
- [ ] List entry offsets for deep micro-lists are pinned, and opening an entry by index returns the expected cell values without walking sibling entries.
- [ ] A test asserts that unclosed non-simple child elements appear in the parent record table while scanning is still in progress inside that element (ADR 0021).
- [ ] A test asserts that mixed content, empty tags, and attribute-only elements retain their current outline representation.
- [ ] All tests run through the `ViewerSession` public seam without asserting internal parser state.
