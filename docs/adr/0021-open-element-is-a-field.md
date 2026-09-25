# An open element is a field before its end tag

A child becomes a field when the scan has seen its start tag and that it has an attribute or an element child. The end tag can still be ahead. A second sibling of that name still promotes the field to a list, and the count includes siblings already seen. The row stays on the record table while the scan continues.

Waiting for the end tag was rejected. A parent with tens of thousands of children would stay off the record for the whole scan, so the screen would show only the siblings that had already closed.
