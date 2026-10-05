# A repeated JSON key stops the scan

A second key with the same name stops the scan. Tables built before that point stay. The repeated key does not become a field. Last-wins, which is what a typical JSON parser does, would drop a value the file contains. Two fields of the same name would be the same-name list ADR-0029 rejected.
