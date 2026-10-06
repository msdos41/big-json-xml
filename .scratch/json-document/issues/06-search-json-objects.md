# 06: Search JSON objects

**What to build:** A person can search a JSON object with the same text search as XML. Values are the shown literals, so the kinds stay distinct. Field names are found with the element-name switch. Choosing a hit expands its path and scrolls that cell into view.

**Blocked by:** 01: Open a JSON object as a record table

**Status:** resolved

- [x] `equals null` hits the scalar `null` and not the string `"null"`. `equals "null"` hits only that string. `contains null` hits both. `equals ""` hits an empty string.
- [x] A collapsed `{}` is not a value, so `{` does not hit every object.
- [x] The element-name switch tests field names. With values turned off, a key is still found. The attribute-name switch matches nothing. A key `@id` is a field name, not an attribute.
- [x] Field name limits use the name as shown. An empty Field name allows every field. An empty Find what has no hits.
- [x] Match case is on by default, so `TRUE` does not match `true`.
- [x] Two matches in one string are two hits. The character offset counts into the literal, including the quotes.
- [x] Choosing a hit expands the embedded tables on its path and scrolls that cell into view. The path is the slash form.
- [x] Viewer session tests cover `equals` and `contains` on `null` and `"null"`, the element-name switch, the attribute-name switch, and revealing a hit.
