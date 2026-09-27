# 08: Show namespaced fields

**What to build:** Fields that come from a namespace show the prefix written in the file. Two prefixes for the same namespace URI and local name are one field. An attribute and an element that share a local name stay distinct.

**Blocked by:** 01: Open a document as a record table

**Status:** resolved

- [ ] A namespaced name is shown with the prefix written in the file.
- [ ] Two prefixes bound to the same URI do not create two fields for the same local name. They are one field.
- [ ] An unprefixed element name in a default namespace uses that default namespace's URI as its identity.
- [ ] An unprefixed element name with no default namespace in scope is in no namespace.
- [ ] `@local` and a child element `local` are two fields, including when both are in the same namespace.
- [ ] Viewer session tests cover distinct prefixes, a default namespace, and `@` versus element, and the window shows the prefixed names.
