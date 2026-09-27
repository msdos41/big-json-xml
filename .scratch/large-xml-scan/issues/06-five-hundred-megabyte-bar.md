# 06: Finish a 500MB scan within the time bar

**What to build:** A person opens a local XML document of about 500MB and the scan finishes in about ten seconds of wall time, whether the document is mostly repeated siblings or mostly distinctly named elements. The window can still take a slice back while the scan runs. The scan runs to the end; it is not cut off when the clock passes ten seconds.

**Blocked by:** 01: Read a file-backed document forward. 03: Keep list entry places after collapse. 04: Keep a closed record as an outline. 05: Keep the window responsive during a scan.

**Status:** resolved

- [x] Two generated documents of about 500MB are written to temporary files and opened through the file-backed session. One is repeated siblings. The other is distinctly named elements.
- [x] Time spent building each file is not part of the measurement. Each scan's wall time fails the test if it exceeds fifteen seconds. The product target is about ten seconds.
- [x] While either document is still scanning, one slice returns within a short bound, on the order of a frame and well under a tenth of a second.
- [x] The repeated-sibling document still opens an entry near the end after the list is collapsed, without reading the other entries again.
- [x] The distinctly named document still opens one nested record with work proportional to that record, from the outline, with no second parsed copy of each closed element.
- [x] The scan is not abandoned at ten seconds. A larger document may take longer. A network path is not the machine under test.
- [x] The 500MB harness sample is not committed and is not required. Carriage-return whitespace is a separate ticket and does not block this one.
