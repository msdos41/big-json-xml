# 04: Stop a JSON scan or refuse a second value

**What to build:** A person can still read the valid prefix of a broken JSON document. A repeated key, an illegal byte, or junk after one finished value stops the scan and leaves the tables already built. A second value, like `{}{}`, does not open at all.

**Blocked by:** 01: Open a JSON object as a record table

**Status:** resolved

- [x] A repeated key stops the scan. The repeated key does not become a field. Tables built before the stop stay, and the stop is marked at that byte. Later rows are not invented.
- [x] A comment, a trailing comma, `NaN`, `+1`, a single-quoted key, and invalid UTF-8 stop the same way.
- [x] `{} oops` keeps the finished object and stops at `o`.
- [x] `{}{}` and `{} {}` do not open. No tables are shown.
- [x] Viewer session tests cover the repeated key, one illegal byte that keeps the prefix, trailing junk, and a second value that does not open.
