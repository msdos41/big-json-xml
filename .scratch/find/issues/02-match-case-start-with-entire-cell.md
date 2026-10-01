# 02: Match case, Start with, and Entire cell

**What to build:** A person can turn Match case off, require the field text to start with Find what, or require the whole field text to equal Find what. The same case switch is the one later name boxes will use.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** ready-for-agent

- [ ] Match case defaults to on, so `World` does not match `world`.
- [ ] Match case off compares by ordinal case-folding.
- [ ] Start with matches only when the whole field text starts with Find what.
- [ ] Entire cell matches only when the whole field text equals Find what, so `123` does not hit `RTM=123`.
- [ ] When Entire cell and Start with are both on, the whole field text must equal Find what.
- [ ] The comparison uses the whole field text, including the part past the visible cell window, and does not assemble a multi-megabyte value into one string.
- [ ] `*` stays an ordinary character.
- [ ] Session tests cover case, prefix, and equality, including a match past the visible window.
