# 10: Reveal a hit

**What to build:** Choosing a hit opens every embedded table on its path, scrolls the document's one surface so that cell is in view, and marks it. A match inside a long scalar shows a 200-character window around the match, without opening the full value.

**Blocked by:** 05: Expand a list inside the cell; 07: Show a scalar window, then the full value; 09: Search the whole document

**Status:** resolved

- [ ] Choosing a hit expands each list field and once-only nested record on its path, including lists that were collapsed.
- [ ] The single scroll surface moves so the matching cell is in view, and that cell is marked as the current hit.
- [ ] When the match is inside a scalar longer than 200 characters, the cell shows a 200-character window around the match rather than the start of the value.
- [ ] Revealing a hit does not expand the full value.
- [ ] The path used to open the cell uses the displayed field name and, for a list, the 1-based position among same-named siblings.
- [ ] Viewer session tests cover a hit inside a collapsed list and a hit inside a long scalar, and the window performs the same reveal.
