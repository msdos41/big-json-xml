# 02: Stop on unsafe or broken XML

**What to build:** Opening an untrusted or broken document cannot read other files or the network. When the XML stops being well-formed, the person still sees the tables built so far and a mark at the point the scan stopped. Nothing after that point is invented.

**Blocked by:** 01: Open a document as a record table

**Status:** resolved

- [x] An external entity reference stops the scan and is reported as an error.
- [x] An external DTD subset is not fetched, and a reference that would require one stops the scan.
- [x] Opening a document does not open a network connection.
- [x] On any other well-formedness error, tables already shown stay on screen and the stop is marked at that point.
- [x] Entries and fields after the error are not invented or repaired.
- [x] Viewer session tests cover these cases, and the window shows the surviving tables and the error.
