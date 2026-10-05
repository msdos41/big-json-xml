# 03: Name a non-object JSON value #value

**What to build:** A person can read a JSON scalar, `null`, or array that is not an object key. That value is the field `#value`: a root scalar, a root array, or a list entry. Mixed arrays stay one list. An object key spelled `#value` is the same field.

**Blocked by:** 02: Show a JSON array of objects as a list table

**Status:** ready-for-agent

- [ ] A root scalar (`1`, `"x"`, `true`, `false`, or `null`) is a record table whose only field is `#value`, shown with the literal from 01.
- [ ] A root array is a list field named `#value`.
- [ ] In a list, a string, number, `true`, `false`, `null`, or array entry occupies the column `#value`. An object entry does not put itself in that column.
- [ ] `[1, {"a": 2}, null, [3]]` is one list. The nested array is a list field in `#value`. `null` is shown as `null`. The object entry's missing `#value` is a blank cell.
- [ ] An object entry `{}` is a row of blank cells, not the scalar `null`.
- [ ] An object key written `#value` shares that column with a non-object entry.
- [ ] `[1, {"a": 2}]` has paths `/#value[1]/#value` and `/#value[2]/a`. `[[1]]` has path `/#value[1]/#value[1]/#value`. A root object's keys stay the first path step.
- [ ] Viewer session tests cover a root scalar, a root array, a mixed array, a nested array, an empty object entry, and a key spelled `#value`.
