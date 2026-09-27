# 01: Read a file-backed document forward

**What to build:** A person opens an XML document from a local disk and the scan reads it forward. A search of that document uses the same forward reading. A generated file large enough that seeking once per character cannot finish in time completes its scan inside a bound the old read would miss. While the document is still scanning, one slice of work returns quickly enough that the window can keep accepting input.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] A file-backed document is scanned by a forward read. The scan does not seek the file once per character, and a peek or a whitespace check does not read those same bytes back from the disk one character at a time.
- [x] A text search and a field search of that document use the same forward reading. Hits stay paths in document order.
- [x] A generated temporary file, large enough that a per-character seek cannot meet the bound, finishes scanning within a bound the old read would miss. Time spent building the file is not counted. An in-memory document alone does not satisfy this check.
- [x] While that document is still scanning, one slice returns within a short bound, on the order of a frame and well under a tenth of a second.
- [x] A scalar window, a slice of a full value, and an entry the outline already knows how to open still read the bytes at that place.
- [x] Small in-memory documents still exercise record tables, list tables, and search. This ticket does not set the about-ten-second bar for a 500MB document, and it does not change the shell's idle gap.
