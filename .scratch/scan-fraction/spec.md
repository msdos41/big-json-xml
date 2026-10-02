# Scan fraction

Status: ready-for-agent

## Problem Statement

After a person opens a document, the main window only shows the word Scanning until the scan ends. A local document of about 500MB takes about ten seconds, and a larger document takes longer. During that time the window still accepts input and the scan is still moving, but the word does not say how far the one forward reading has reached or how much longer it is expected to take.

## Solution

While the viewer is scanning a document whose length is fixed and greater than zero, the main window's top row shows a short determinate bar after the word Scanning. The bar is the scan fraction: file bytes already reached, divided by the document's length. After this scan has been running for one second of wall time and at least one byte has been reached, the same row shows the remaining time after the bar, as a short English sentence such as `12s remaining` or `<1s remaining`.

The words Scanning, Complete, and Stopped stay as they are. Complete removes the bar and the remaining time together. Stopped leaves the bar where the scan stopped, looking the same and no longer moving, and removes the remaining time. The error line at the bottom is unchanged. A length of zero, or a length that is not fixed yet, shows neither the bar nor the remaining time. The scan's time budget is unchanged.

## User Stories

1. As a person inspecting a document, I want a short determinate bar on the main window's top row while the scan runs, so that Scanning is not the only sign that the document is still being read.
2. As a person inspecting a document, I want that bar placed after the status word, so that Open, Find, and the status word stay where they are.
3. As a person inspecting a document, I want the bar to be the scan fraction, so that its fill is the file bytes already reached divided by the document's length.
4. As a person inspecting a document, I want the bar as soon as the length is fixed and greater than zero, so that I do not wait a second before seeing it.
5. As a person inspecting a document, I want an empty bar when no file byte has been reached yet, so that the start of the scan is visible.
6. As a person inspecting a document, I want the bar to advance as the scan reaches more file bytes, so that I can see the reading move.
7. As a person inspecting a document, I want the bar to follow file bytes rather than a count of fields or entries, so that one huge scalar and many small elements agree when they cover the same bytes.
8. As a person inspecting a document, I want no percent beside the bar, so that the row does not repeat the fraction as a number.
9. As a person inspecting a document, I want no text inside the bar, so that the fraction stays a bar.
10. As a person inspecting a document, I want the remaining time written after the bar, so that the estimate sits with the fraction it explains.
11. As a person inspecting a document, I want the remaining time hidden until this scan has run for one second, so that the first slice does not show a wild estimate.
12. As a person inspecting a document, I want the remaining time hidden when no file byte has been reached, so that a stalled start does not invent a speed.
13. As a person inspecting a document, I want the remaining time to be the wall-clock time still expected for the bytes ahead, so that the sentence matches when the scan will finish.
14. As a person inspecting a document, I want that estimate to use how fast the scan has read since it started, so that the whole scan shares one rate.
15. As a person inspecting a document, I want an estimate of at least one second rounded up to a whole second, so that `1.01` seconds is shown as `2s remaining`.
16. As a person inspecting a document, I want an estimate of exactly one second to read `1s remaining`, so that a whole second is not rounded further up.
17. As a person inspecting a document, I want an estimate below one second to read `<1s remaining`, so that a scan that is almost finished does not say `0s remaining`.
18. As a person inspecting a document, I want a long estimate to stay in seconds, so that `100s remaining` does not switch into minutes or hours.
19. As a person inspecting a document, I want the sentence to use the word remaining, so that it is the remaining time and not an elapsed time.
20. As a person inspecting a document, I want the idle gap between slices counted in that wall time, so that yielding the window does not make the estimate too short.
21. As a person inspecting a document, I want a text search that shares the scan's pump counted in that wall time, so that searching while the scan runs makes the remaining time longer.
22. As a person inspecting a document, I want the remaining time to grow when file bytes pause and the wall clock moves, so that a stall is not shown as nearly done.
23. As a person inspecting a document, I want an internal entity's expansion to hold the scan fraction at that reference in the file, so that the bar does not run through the replacement text.
24. As a person inspecting a document, I want the remaining time to grow during that expansion and to come back once file bytes move again, so that the pause and the recovery are both visible.
25. As a person inspecting a document, I want Complete to remove the bar and the remaining time in the same update, so that a finished scan is only the word Complete.
26. As a person inspecting a document, I want the remaining time never to read `0s remaining`, so that finishing is Complete and not a zero estimate.
27. As a person inspecting a document, I want a document that finishes in the first slice to show Complete with no bar, so that a small open does not flash a full bar.
28. As a person inspecting a document, I want a well-formedness stop on a document of length greater than zero to leave the bar at the bytes reached, so that I can see where the scan stopped.
29. As a person inspecting a document, I want that stopped bar to look the same as it did while scanning, so that Stopped and the error line are what say it stopped.
30. As a person inspecting a document, I want that stopped bar to stay put across later refreshes, so that it does not jump to the end or disappear.
31. As a person inspecting a document, I want the remaining time removed when the scan stops, so that a stopped document has no estimate of a finish.
32. As a person inspecting a document, I want the status word to become Stopped, so that the existing word still names the outcome.
33. As a person inspecting a document, I want the bottom error line to keep the message, the line, and the column, so that the bar does not replace the error.
34. As a person inspecting a document, I want a stop at the first byte of a non-empty document to leave an empty bar, so that a failure before any byte is a fraction of zero.
35. As a person inspecting a document, I want a stop close to the end to leave a nearly full bar, so that the fraction still says how much of the file was read.
36. As a person inspecting a document, I want a document of length zero to show only Stopped and the error, so that a ratio with no denominator is not drawn.
37. As a person inspecting a document, I want a source whose length is still growing to show Scanning with no bar and no remaining time, so that an unfinished length is not a fraction.
38. As a person inspecting a document, I want the bar to appear once that length becomes fixed and greater than zero while the scan is still running, so that the fraction starts only when the denominator is real.
39. As a person inspecting a document, I want opening another document to replace the bar and restart the one-second gate, so that the row belongs to the scan now running.
40. As a person inspecting a document, I want the status words to remain Scanning, Complete, and Stopped, so that existing reading of those words stays true.
41. As a person inspecting a document, I want to scroll and search while the bar is moving, so that the window stays usable throughout the scan.
42. As a person inspecting a document, I want the scan to run to the end of the document, so that the bar does not cut the reading off.
43. As a person inspecting a document, I want each slice to stay as short as it is today, so that drawing the bar does not freeze the window.
44. As a person inspecting a document, I want a local document of about 500MB to keep the about-ten-second budget, so that the bar does not relax that bar.
45. As a person inspecting a document, I want a larger document to take longer and to show the fraction and the remaining time on the same rule, so that size does not invent a second kind of scan.
46. As a person inspecting a document, I want an external entity to stop the scan as it does today, so that the bar freezes at that stop like any other well-formedness error.
47. As a person inspecting a document, I want the find window and the results window to stay without this bar, so that the fraction lives on the main window only.
48. As a person inspecting a document, I want the viewer to stay read-only, so that showing the fraction does not change the document.
49. As a person inspecting a document, I want tables, the outline, hits, and paths to stay as they are, so that the bar is chrome on the scan and not a new reading of the document.
50. As a person inspecting a document, I want each refresh during Scanning to follow the bytes reached so far, so that the bar and, when shown, the remaining time match this slice.

## Implementation Decisions

- This work is chrome on the existing scan. The desktop shell model publishes three observations: whether the scan fraction is shown, the fraction as a number from 0 through 1, and the remaining-time text, which is empty when it is hidden. The main window's top row binds a short determinate bar to that fraction and binds the remaining-time text after the bar. The bar has a short fixed width beside the status word. It does not stretch across the row, and it does not cover the document.
- The viewer session reports the file bytes the scan has reached, the document length, and whether that length is fixed. It does not choose the sentence or draw the bar. Tests do not talk to the session or the parser for this spec. They talk to the shell model.
- Bytes reached are the position in the file. While an internal entity is expanded, that position stays at the entity reference in the file. The replacement text is not added to the fraction. Bytes reached do not run past the document length.
- A file opened from a seekable stream has a fixed length at open. A source that is still being appended does not have a fixed length until it is completed. A fixed length of zero has no scan fraction.
- The fraction is shown only while the length is fixed and greater than zero and the scan is Scanning or Stopped. It is hidden on Complete, on a length of zero, and while the length is not fixed. A fraction of zero is shown as an empty bar when those conditions say the fraction exists. Complete and the hiding of the bar happen in the same refresh. A document that finishes inside the first slice publishes Complete with the fraction hidden, and never a full bar.
- Remaining time is computed in the shell from whole file bytes and whole milliseconds of wall time since this scan started. Production takes that wall time from the clock already used to bound slices, so the idle gap between slices and a text search sharing the pump are both included. A test may supply the elapsed milliseconds to the same shell model. When it does, the text uses that value. Supplying it is driving this model, not a second module.
- The text is empty unless the scan is Scanning, the fraction is shown, the elapsed time is at least 1000 milliseconds, and at least one byte has been reached. Otherwise the estimate is the real quotient `(length - bytes reached) × elapsed milliseconds / bytes reached`. A quotient greater than 0 and less than 1000 is the text `<1s remaining`. A quotient of 1000 or more is `Ns remaining`, where N is the smallest whole number of seconds that covers the quotient. Exactly 1000 milliseconds of estimate is `1s remaining`. Exactly 2000 is `2s remaining`. Anything above a whole second rounds up. The number is a plain decimal integer with no grouping marks. The word is `remaining`. The text `0s remaining` is never published. A refresh that reaches the end of the document publishes Complete with an empty text instead.
- Stopped with a length greater than zero keeps the fraction at the bytes reached when the scan stopped, keeps the same bar, and publishes an empty remaining time. Later refreshes do not move that fraction. The status word is Stopped. The error line keeps the message, line, and column it already publishes. Stopped at a length of zero publishes no fraction and an empty text.
- Opening a document replaces the previous scan, hides the previous fraction and text, and starts the one-second gate from the new scan.
- The status words remain exactly Scanning, Complete, and Stopped. Slice length and the idle gap do not change. The scan still runs to the end of the document. ADR 0025 is unchanged. There is no second pass over the file to measure the fraction.
- The find window and the results window do not gain a bar or a remaining time. The bar does not change color, icon, or caption when the scan stops. There is no percent, no byte-count tooltip, and no elapsed-time sentence.

## Testing Decisions

- One seam: the desktop shell model. A test opens a document, lets the scan run in the existing slices, and reads the status word, the error line, whether the scan fraction is shown, the fraction, and the remaining-time text. It may supply the elapsed milliseconds used for that text. It does not open a second seam on the session, the parser, or the window's control tree.
- A good test asserts behavior a person can observe on that model. It asserts the exact remaining-time sentence, whether the bar's fraction is shown, and that the status word and the error line are unchanged in form. It does not assert pixel widths, control types, private cursors, or buffer sizes.
- Prior art is the shell tests that open a path or an in-memory source, tick while the status is Scanning, and read Status, Error, and the rows. Follow that shape. Rule cases use small documents. A partial scan uses a document that does not finish in one slice. Do not add a 500MB fixture for this spec. The ten-second budget already has its own tests and is not re-measured here.
- Assert the visibility matrix. A fixed length greater than zero shows a fraction while Scanning, including a zero fraction before any byte is reached. Elapsed time under one second leaves the text empty. Elapsed time of at least one second with no byte reached leaves the text empty. A growing source shows neither until its length is fixed. A length of zero ends Stopped with neither, and with the existing no-root error. Complete hides both in that same observation and leaves the word Complete. A tiny document that finishes immediately is that Complete case.
- Assert the sentence from the published fraction and a supplied elapsed time, choosing an elapsed value whose estimate sits comfortably inside a range so rounding is not on a boundary. Cover `<1s remaining`, an exact whole second as `Ns remaining`, a value that rounds up to the next second, and a three-digit second count such as `100s remaining`. Assert that a longer elapsed time with the same bytes reached produces a longer sentence. Assert that the text does not contain `left`, a percent, or `0s remaining`.
- Assert a stop in the middle of a non-empty document: the word is Stopped, the error line is still the message with its line and column, the fraction stays strictly between 0 and 1, a later slice does not change it, and the text is empty. Assert a failure before any byte as a shown fraction of zero. Assert an external-entity stop the same way, as a stop rather than as a new rule.
- Assert an internal entity large enough that one slice does not finish expanding it. Across that stall the fraction stays the same, and a longer supplied elapsed time makes the remaining time longer. After the scan reaches the end, the fraction and the text are gone.
- Assert that opening a second document clears the previous fraction and text and starts a new one-second gate. Assert that a text search started during a scan still reports hits, and that the status word is still Scanning. The wall-clock rule itself is the elapsed-time assertion above. Do not race a real search against the clock to prove the sentence grew.

## Out of Scope

- Changing the about-ten-second budget, the slice length, or the idle gap in ADR 0025.
- Cancelling or pausing the scan from the bar.
- A percent, a byte count, an elapsed time, a minute or hour format, a tooltip, or a different color or icon when the scan stops.
- A bar on the find window or the results window.
- An indeterminate bar while a source is still growing.
- A second reading of the document whose purpose is to measure the fraction.
- Parsing or presenting JSON.
- Editing, saving, or any other change to the document.
- Changing tables, the outline, text search, paths, or how a hit is revealed.
- A window control-tree test, a parser test, or a session test as a seam for this behavior.

## Further Notes

- The names in `CONTEXT.md` are the names to use. This spec uses Document, Scan, Scan fraction, Remaining time, Viewer, and Text search. Scan fraction avoids Progress, percent complete, and load progress. Remaining time avoids ETA, countdown, and time left.
- ADR 0025 still says a local document of about 500MB finishes in about ten seconds, the window keeps accepting input, and the scan runs to the end. This spec adds the bar and the remaining time on top of that budget. ADR 0017 still expands internal entities and stops on an external entity. ADR 0002 still leaves JSON out.
- The agreed test seam is the desktop shell model. The session only supplies the file position and the length so the shell can publish the fraction.
