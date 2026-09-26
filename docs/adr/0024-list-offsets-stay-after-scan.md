# List entry offsets stay after the scan

The scan records a file offset for every list entry as it sees that entry, and collapsing the list does not drop those offsets. Opening an entry uses the stored offset.

This supersedes ADR 0016's rule that offsets exist only while a list is expanded. That rule made the first expand of a long list read every entry again, and collapse threw the offsets away. A full-document element index is still rejected: search keeps a path and does not read this offset list.
