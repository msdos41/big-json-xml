# A closed record keeps an outline, not a resident body

The scan keeps each field's name, kind, and place, and each list entry's place (ADR 0024). It does not keep a second parsed copy of a closed record. Opening a record uses that outline. A list inside it uses the counts, columns, and entry places the scan already stored.

Keeping a parsed body for every closed record was rejected. A document of millions of distinct elements would spend the scan on that tree and miss the time in ADR 0025. Dropping list entry places was also rejected (ADR 0024).
