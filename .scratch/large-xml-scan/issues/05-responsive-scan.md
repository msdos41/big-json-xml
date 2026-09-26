# 05: Keep the window responsive during a scan

**What to build:** While a document is scanning, the window keeps accepting input. The scan returns often enough that input is handled several times a second, and the idle gap between slices is not longer than the slice itself. A scan whose own work takes a few seconds can therefore finish in about ten seconds of wall time.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] While a scan is in progress, one slice of work returns within a short bound, on the order of a frame and well under a tenth of a second.
- [ ] The shell's idle time between slices is not longer than the slice itself, so the scan is not limited to a small fraction of the wall clock.
- [ ] Input is handled at least several times a second during the scan. The window does not go unresponsive for seconds at a time.
- [ ] The Viewer session remains the test seam. The shell schedule is still required; a window test is not added.
- [ ] This ticket does not by itself require a 500MB document to finish within fifteen seconds.
