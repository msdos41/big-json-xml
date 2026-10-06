# 04: Field name

**What to build:** A person can limit a text search to one field name, written as the table shows it. An empty field name allows every field. This is how a named field is searched now that the query string is gone.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** resolved

- [x] An empty Field name does not limit the search.
- [x] Field name `Start_node` and Find what `123` hit a simple `Start_node` whose text contains `123`.
- [x] Field name `#text` hits character data only when that element also has an attribute or a child element.
- [x] Field name `Start_node` does not hit that `#text`.
- [x] Field name uses the displayed spelling, including `@` and a prefix from the file, and follows the Match case switch.
- [x] Entire cell plus a field name requires the whole value to equal Find what.
- [x] Session tests cover the simple element, `#text`, an attribute name, and Match case off.
