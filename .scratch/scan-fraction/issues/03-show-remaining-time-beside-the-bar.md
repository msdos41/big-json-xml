# 03: Show remaining time beside the bar

**What to build:** While the viewer is scanning, the fraction is shown, this scan has already run for one second of wall time, and at least one file byte has been reached, the top row shows the remaining time after the bar. An estimate of at least one second is a whole number of seconds rounded up, written `12s remaining`. An estimate below one second is `<1s remaining`. A long estimate stays in seconds, such as `100s remaining`. The sentence is absent before one second, when no byte has been reached, on Complete, and on Stopped. It never reads `0s remaining`. A longer wall time with the same bytes reached makes the sentence longer. Opening another document starts the one-second gate over.

**Blocked by:** 01: Show the scan fraction while scanning

**Status:** resolved

- [x] The sentence sits after the bar. It uses the word remaining. It does not say left, it does not include a percent, and it is not drawn inside the bar.
- [x] The text is empty until this scan's wall time has reached one second and at least one file byte has been reached.
- [x] The estimate is the wall-clock time for the bytes still ahead, using how fast the scan has read since it started. Exactly one second of estimate reads `1s remaining`. A value above a whole second rounds up, so just over one second reads `2s remaining`. Below one second reads `<1s remaining`. A three-digit count stays in seconds, such as `100s remaining`, with no grouping marks.
- [x] The same bytes reached and a longer elapsed time produce a longer sentence. That elapsed time is wall time, so the idle gap between slices and a text search sharing the pump both count.
- [x] Complete removes the sentence in the same update as the word Complete. Stopped removes the sentence. The text `0s remaining` is never shown.
- [x] Opening another document does not keep the previous scan's elapsed time. The new scan hides the sentence until its own first second has passed.
- [x] A text search started while the scan is running still reports hits, and the status word stays Scanning.
- [x] A test may supply the elapsed milliseconds to the desktop shell model and assert the exact sentence. It does not sleep for a second, and it does not race a real search against the clock.
- [x] Tests talk only to the desktop shell model. This ticket does not change where a stopped bar sits.
