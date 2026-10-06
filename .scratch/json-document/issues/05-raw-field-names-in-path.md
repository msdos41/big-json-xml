# 05: Write JSON field names raw in the path

**What to build:** A person can open a JSON object whose key is empty or contains `/` or brackets. The key is one field. The path writes that name as itself and is not parsed back into steps.

**Blocked by:** 01: Open a JSON object as a record table

**Status:** resolved

- [x] `{"": 1}` opens. The field name is empty, so the header is blank, and the value is the number `1`.
- [x] `{"a/b": 1}` is one field named `a/b`, not a field `a` with a child `b`.
- [x] `{"a[1]": 1}` is one field named `a[1]`, not a list entry.
- [x] The path writes those names as themselves. Revealing the field opens that field.
- [x] Viewer session tests cover the empty key, the slash, and the brackets.
