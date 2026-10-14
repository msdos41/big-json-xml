# 01: Record the baseline for deeply nested multi-list documents

**What to build:** A person inspecting a deeply nested, multi-list XML document can read, on the spec, how long the current viewer takes before optimization. The clocks are the viewer session scan and sequential disk read, measured in Release builds as the median of three successive runs in one process. The table records the document bytes, sequential read milliseconds, scan milliseconds, and the peak working set after the scan. The existing timing set files and the new benchmark document are both measured. Nothing gets faster yet.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Every document in the target benchmark set, including `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` and representative timing set documents, is measured through the viewer session. The documents stay outside the repository.
- [ ] The scan clock runs from the start of the forward reading to the completed outline. Process start, file dialogs, and painting stay outside the clock.
- [ ] Sequential read is measured on the same machine with a 1MB buffer, median of three runs, including file open.
- [ ] Peak working set is recorded after the scan completes and before search.
- [ ] The baseline numbers are appended to the spec. The viewer code does not change in this ticket.
