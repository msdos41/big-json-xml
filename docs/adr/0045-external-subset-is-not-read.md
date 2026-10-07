# An external DTD subset is not read

A DOCTYPE may name an external subset with SYSTEM or PUBLIC. The viewer skips that identifier. It does not fetch the subset, does not open the path, and does not connect to a network. The document after the DOCTYPE is scanned. Entities the subset would have declared stay undefined, and a reference to one of them stops the scan (ADR 0017). An internal subset in the same DOCTYPE is still read, and its internal entities are still expanded. An external entity declared in that internal subset still stops the scan when it is referenced.

Stopping at the identifier was rejected. Documents that only name a DTD, and do not need it to be well-formed, never opened. Fetching the subset was rejected because that reads a file or a network the document points at.
