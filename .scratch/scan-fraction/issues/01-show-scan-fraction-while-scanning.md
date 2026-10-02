# 01: Show the scan fraction while scanning

**What to build:** While a document is scanning and its length is fixed and greater than zero, the main window's top row shows a short determinate bar after the word Scanning. The bar is the scan fraction: file bytes already reached, divided by the document's length. No byte reached yet leaves an empty bar. The bar advances as the scan reaches more of the file, and it stays put while an internal entity is expanded. Complete removes the bar in the same update. A document that finishes in the first slice shows Complete and no bar. A source whose length is still growing shows Scanning and no bar. Opening another document replaces the bar with that new scan.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] The bar sits on the main window's top row after the status word. It is short, determinate, and does not cover the document. The find window and the results window do not grow a bar.
- [ ] The fill is file bytes reached divided by the document length, not a count of fields or entries. Zero bytes reached is an empty bar. The fill moves forward as more of the file is read and does not run past the end.
- [ ] While an internal entity is expanded, the fill stays at that reference in the file. The replacement text is not added to the fraction.
- [ ] The bar shows no percent and no text inside it.
- [ ] Complete hides the bar in the same update that sets the status word to Complete. A small document that finishes in the first slice never shows the bar.
- [ ] A source that is still being appended shows Scanning and no bar. Once that length is fixed and greater than zero and the scan is still running, the bar appears.
- [ ] Opening another document drops the previous bar and shows the new scan's fraction under the same rules.
- [ ] The status words stay Scanning and Complete. Scrolling and text search still work while the bar is moving. The slice length, the idle gap, and the about-ten-second budget stay as they are.
- [ ] Tests talk only to the desktop shell model. They do not assert pixel widths, control types, or a parser cursor.
