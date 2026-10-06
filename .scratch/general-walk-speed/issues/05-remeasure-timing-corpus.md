# 05: Remeasure the timing corpus

**What to build:** A person inspecting the same example documents as before waits less. The local employee JSON and XML documents and the three `.vxf` documents are timed again through the viewer session. Scan and text search both finish sooner than the baseline in the parent spec. The working set after the scan stays on the order of a few tens of megabytes. Hits are unchanged. A large document of distinct field names is not slower than it is today. The new times are written onto the spec. They do not become a new bar in the suite, and the documents are not committed.

**Blocked by:** 02: Finish an XML scan sooner. 03: Finish a JSON scan sooner. 04: Finish a text search sooner.

**Status:** ready-for-agent

- [ ] Each example document is scanned to completion through the viewer session. The wall time is lower than the baseline recorded in the parent spec.
- [ ] A text search of each example document finishes in less wall time than that baseline. Hit count, path, and match stretch match the search of that same document before this work.
- [ ] After the scan, and again after the search, the working set stays on the order of a few tens of megabytes. The managed heap grows with list-entry offsets and with the hit list, not with a second copy of the document.
- [ ] The distinct-name document of about 500MB still finishes within the existing fifteen-second bar.
- [ ] The new times are appended to the parent spec. The suite does not encode them as a seconds target.
- [ ] The example documents stay outside the repository.
