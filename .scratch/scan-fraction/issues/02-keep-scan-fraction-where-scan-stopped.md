# 02: Keep the scan fraction where the scan stopped

**What to build:** When a scan stops on a well-formedness error in a document whose length is greater than zero, the bar stays at the file bytes reached. It looks the same as it did while scanning, and later refreshes do not move it. The status word is Stopped, and the error line at the bottom still carries the message, the line, and the column. A document of length zero shows only Stopped and that error. A stop before any byte leaves an empty bar.

**Blocked by:** 01: Show the scan fraction while scanning

**Status:** resolved

- [x] A stop in the middle of a non-empty document leaves the bar strictly between empty and full. A later slice does not change that fill, does not fill the bar, and does not remove it.
- [x] The stopped bar looks the same as the scanning bar. There is no new color, icon, or caption.
- [x] A stop before any file byte has been reached leaves an empty bar on screen.
- [x] A document of length zero ends Stopped with the existing no-root error, and with no bar.
- [x] The status word is Stopped. The error line still shows the message, the line, and the column.
- [x] An external entity stops the scan the same way. The bar freezes at that stop and the error line is unchanged in form.
- [x] This ticket does not add a remaining-time sentence.
- [x] Tests talk only to the desktop shell model.
