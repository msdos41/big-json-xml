# 11: Ship a Windows single-file executable

**What to build:** A person can copy one Windows executable and run the viewer: open a document, expand a list, search, and reveal a hit. No separate runtime install is required.

**Blocked by:** 02: Stop on unsafe or broken XML; 06: Jump within a long list; 10: Reveal a hit

**Status:** resolved

- [ ] The shipping build is one self-contained single-file executable for Windows, on CoreCLR, not Native AOT.
- [ ] The Viewer session and the document scan do not call Win32 APIs.
- [ ] Running that executable can open a document as a record table, expand a list, search, and reveal a hit.
- [ ] macOS and Linux builds are not produced or tested for this ticket.
