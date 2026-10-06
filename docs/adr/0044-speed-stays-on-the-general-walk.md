# Speed stays on the general walk

Scan and text search get faster by doing less temporary work on the forward reading they already do. The employee JSON, employee XML, and `.vxf` files are a timing corpus. The walk does not key off their element names, their depths, or their schemas. Every document the viewer already opens uses that same walk at every depth. A repeated list and a tree of distinct names may still cost differently, because a list keeps an entry offset and a closed record keeps an outline without a second body (ADR 0024, ADR 0026). JSON and XML stay separate grammars (ADR 0029).

After the scan, memory stays in the band already measured for these files. The working set is on the order of a few tens of megabytes. The managed heap grows with list-entry offsets and with the hit list. A resident search index stays rejected (ADR 0024). Hits keep their path, match offset, match length, and document order for every text-search condition. The window still accepts input during the scan (ADR 0025).

A seconds target is not part of this decision. The change is checked by remeasuring the timing corpus, keeping that memory band, keeping hits, and not slowing a large document of a different shape. A walker specialized to these samples was rejected. Spending retained memory on an index, in exchange for skipping the second reading, was rejected.
