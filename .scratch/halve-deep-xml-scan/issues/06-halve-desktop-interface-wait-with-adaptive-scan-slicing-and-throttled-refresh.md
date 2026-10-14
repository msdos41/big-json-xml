# 06: Halve desktop interface wait with adaptive scan slicing and throttled refresh

**What to build:** The desktop viewer dynamically increases the background scan work slice to 64 milliseconds with a 4-millisecond idle yield whenever user input has been quiescent for at least 250 milliseconds. Any mouse move, click, scroll, or keystroke immediately reverts the work slice to 32 milliseconds with a 16-millisecond yield, ensuring immediate UI responsiveness. Surface layout and refresh during scanning are throttled to thirty frames per second, preventing redundant frame reads while maintaining continuous scan fraction and remaining time updates. The wall-clock loading wait experienced in the desktop shell drops by half.

**Blocked by:** 05: Streamline consecutive SameShape flat scanning.

**Status:** resolved

- [x] `MainWindow` monitors pointer move, pointer press, scroll wheel, and key events, updating a last-interaction timestamp.
- [x] When user interaction has been idle for at least 250 milliseconds, `ViewerModel.Tick()` increases scan work duration up to 64 milliseconds and reduces idle interval to 4 milliseconds.
- [x] Any user interaction event immediately drops the scan slice back to 32 milliseconds and sleep interval to 16 milliseconds.
- [x] During active scanning, calls to `Refresh()` from `Tick()` are throttled to at most once every 33 milliseconds (30 fps).
- [x] The scan fraction and remaining time text continue updating smoothly during the scan.
- [x] A slice of scan work continues returning well under one-tenth of a second.
- [x] The total wall-clock time from opening `4d7480cde29a662758904cbe0e1afdbb-BD_.xml` to scan completion in the desktop viewer is cut in half compared to the baseline in ticket 01.
