# JSON document

Status: ready-for-agent

## Problem Statement

The viewer opens an XML document as record tables and list tables, and can scroll and search it without building a tree of objects. A JSON file does not open. The person has the same job for a large JSON document: read it, expand a list, and search it, without editing it and without a second product.

## Solution

A JSON file is a Document in the same read-only Viewer. After a leading BOM and whitespace, the first byte chooses the grammar. `<` is XML. A byte that can start a JSON value is JSON. The file name does not choose.

A JSON object that is not a list entry is a record table of its keys, in file order. An array of any length, including zero, is a list field. A value that is not an object key is the field `#value`. Scalars are shown as JSON literals, so string, number, true, false, and null stay distinct. Search, path, scan fraction, and scan stop are the same Viewer behaviors, with the JSON rules below. XML documents keep the XML table rules.

## User Stories

1. As a person inspecting a document, I want to open a JSON document in the same Viewer as an XML document, so that I do not learn a second product.
2. As a person inspecting a document, I want the document to stay read-only, so that opening it cannot change the file.
3. As a person inspecting a document, I want the first table to appear before the scan finishes, so that a large file is usable before the last byte.
4. As a person inspecting a document, I want one forward scan, so that the viewer does not build a tree of objects.
5. As a person inspecting a document, I want the grammar chosen by the first byte after a leading BOM and whitespace, so that a missing or wrong suffix still opens.
6. As a person inspecting a document, I want a first byte `<` to open as XML, so that a `.json` file whose body is XML is still the XML document.
7. As a person inspecting a document, I want `{`, `[`, `"`, `-`, a digit, `t`, `f`, or `n` to open as JSON, so that a `.xml` file or a file with no suffix can still be a JSON document.
8. As a person inspecting a document, I want any other first byte to refuse the file, so that a text file is not guessed into a table.
9. As a person inspecting a document, I want the file name to be ignored when choosing the grammar, so that the suffix is only a dialog filter.
10. As a person inspecting a document, I want a JSON document to be UTF-8, so that a UTF-16 file is not silently misread.
11. As a person inspecting a document, I want a leading BOM ignored, so that a BOM does not become a field or a syntax error.
12. As a person inspecting a document, I want an empty file to stay closed, so that there is no empty table pretending to be a value.
13. As a person inspecting a document, I want a file of only whitespace to stay closed, so that blanks are not a value.
14. As a person inspecting a document, I want `{}{}` to stay closed, so that two values are not one Document.
15. As a person inspecting a document, I want whitespace between two values, as in `{} {}`, to stay closed, so that the gap does not make the second value legal.
16. As a person inspecting a document, I want `{} oops` to keep the finished object and stop at `o`, so that junk after one value is not treated as a second value.
17. As a person inspecting a document, I want a comment, a trailing comma, `NaN`, `+1`, or a single-quoted key to stop the scan at the first illegal byte, so that JSON5 is not accepted.
18. As a person inspecting a document, I want the tables built before that stop to stay, so that the valid prefix is still readable.
19. As a person inspecting a document, I want the stop marked at that byte, so that I can see the tables do not cover the rest of the file.
20. As a person inspecting a document, I want the scan not to invent rows past the stop, so that a broken tail does not become fields.
21. As a person inspecting a document, I want invalid UTF-8 to stop the scan the same way, so that a bad byte is not a character.
22. As a person inspecting a document, I want whitespace between JSON tokens to be ignored, so that pretty-printing does not create fields.
23. As a person inspecting a document, I want a root object to be a record table of its keys, so that one object reads down the side.
24. As a person inspecting a document, I want `{}` to be a record table with no fields, so that an empty object is still an object.
25. As a person inspecting a document, I want a collapsed JSON object to show `{}`, with or without keys, so that the cell is visibly an object.
26. As a person inspecting a document, I want expanding that cell to show the record table inside the cell, so that I do not leave the current surface.
27. As a person inspecting a document, I want keys in the order they appear in the file, so that the table matches the file rather than a sorted list.
28. As a person inspecting a document, I want each key to be one field, so that a JSON object is not a list of pairs.
29. As a person inspecting a document, I want the field name to be the decoded key, with no leading `@` and no added quotes, so that the name is the key itself.
30. As a person inspecting a document, I want an empty key to be a field with a blank header, so that `{"": 1}` still opens.
31. As a person inspecting a document, I want a key that contains `/` or brackets to stay one field, so that `a/b` is not two steps.
32. As a person inspecting a document, I want a repeated key to stop the scan, so that `{"a": 1, "a": 2}` does not silently drop a value.
33. As a person inspecting a document, I want the repeated key not to become a field, so that the object does not grow a second `a`.
34. As a person inspecting a document, I want a nested object to be a record field, collapsed as `{}`, so that objects inside objects stay record tables.
35. As a person inspecting a document, I want a root scalar to be a record table whose only field is `#value`, so that `1`, `"x"`, `true`, `false`, and `null` have a place to sit.
36. As a person inspecting a document, I want a root array to be a list field named `#value`, so that the document has a name for its path.
37. As a person inspecting a document, I want an array of any length, including zero and one, to be a list field, so that `[1]` is not the scalar `1` and `[]` is not a blank scalar.
38. As a person inspecting a document, I want that list to be a list as soon as the array is recognized, so that the table does not wait for a second element and does not demote later.
39. As a person inspecting a document, I want a collapsed array to read `Name[n]`, so that `a[0]` is an empty list and `a[1]` is a list of one.
40. As a person inspecting a document, I want `n` to grow as entries are seen, from the scan already walking the array, so that the count is not a second pass.
41. As a person inspecting a document, I want the entries of one array to be one list even when their kinds differ, so that `[1, {"a": 2}, null, [3]]` is one list table.
42. As a person inspecting a document, I want object entries to contribute their keys as columns, in the order first seen, so that a later key is added at the end.
43. As a person inspecting a document, I want a string, number, true, false, null, or array entry to occupy the column `#value`, so that a non-object has a cell.
44. As a person inspecting a document, I want a nested array in that column to be a list field, so that `[[1]]` expands inside `#value`.
45. As a person inspecting a document, I want an object key written `#value` to share that column, so that there is one field of that name.
46. As a person inspecting a document, I want a list entry that is an object not to also be drawn as a record table, so that its keys are the columns of the row.
47. As a person inspecting a document, I want `{}` as a list entry to be a row of blank cells, so that it is not the scalar `null`.
48. As a person inspecting a document, I want a missing key on a list row to be a blank cell, so that absence is not the word `null` and not `""`.
49. As a person inspecting a document, I want the first entry's path index to be 1, so that `Name[1]` is the first entry and `Name[0]` is only the empty-list label.
50. As a person inspecting a document, I want `{"a": {"b": 1}}` to have the path `/a/b`, so that a root object adds no extra step.
51. As a person inspecting a document, I want `[1, {"a": 2}]` to have the paths `/#value[1]/#value` and `/#value[2]/a`, so that the root array and the scalar entry are both `#value`.
52. As a person inspecting a document, I want `[[1]]` to have the path `/#value[1]/#value[1]/#value`, so that each array adds a list step.
53. As a person inspecting a document, I want a path to write a field name as itself, including an empty name or a name containing `/`, so that the path is not an escaped query.
54. As a person inspecting a document, I want that written path not to be parsed back into steps, so that a slash inside a key does not have to be executable.
55. As a person inspecting a document, I want a JSON string shown in double quotes, with its text decoded, so that the cell is visibly a string.
56. As a person inspecting a document, I want a quotation mark or backslash inside a string written as `\"` or `\\`, so that the surrounding quotes still mark one string.
57. As a person inspecting a document, I want other escapes decoded, so that `\n` is a newline, `\u0041` is `A`, and `\/` is `/`.
58. As a person inspecting a document, I want a number shown as in the file, so that `1.0`, `1e2`, and `-0` are not rewritten.
59. As a person inspecting a document, I want `true`, `false`, and `null` shown as in the file, without quotes, so that each kind stays distinct from a string.
60. As a person inspecting a document, I want an empty string shown as `""`, so that it is distinct from a missing key and from `null`.
61. As a person inspecting a document, I want the string `null` shown as `"null"`, so that it is not the scalar `null`.
62. As a person inspecting a document, I want the string `1` shown as `"1"`, and the string `true` shown as `"true"`, so that those kinds do not collapse together.
63. As a person inspecting a document, I want the full value of a scalar to be that same literal, quotes included, wrapping to the cell width, so that expanding a string does not strip its type.
64. As a person inspecting a document, I want a collapsed scalar to show one width-limited window of that literal, so that a huge string does not become one allocated text.
65. As a person inspecting a document, I want only the visible slice of a scalar decoded, so that search and display do not build one string of the whole value.
66. As a person inspecting a document, I want to search the whole JSON document, including tables that are not expanded, so that a hit does not depend on what I have opened.
67. As a person inspecting a document, I want a value search to test the shown literal, so that `equals null` hits `null` and not the string `"null"`.
68. As a person inspecting a document, I want `equals "null"` to hit only that string, so that the quotes select the string kind.
69. As a person inspecting a document, I want `equals ""` to hit an empty string, so that the two quotes are the whole value.
70. As a person inspecting a document, I want `contains null` to hit both `null` and `"null"`, so that a substring still sees the letters inside the quotes.
71. As a person inspecting a document, I want a missing key to add no value hit, so that a blank cell is not `null` or `""`.
72. As a person inspecting a document, I want `{}` and a collapsed `Name[n]` not to count as values, so that `{` does not hit every object.
73. As a person inspecting a document, I want the element-name switch to test JSON field names, including keys and `#value`, so that a name search still finds fields when values are turned off.
74. As a person inspecting a document, I want the attribute-name switch to match nothing in a JSON document, so that a key `@id` stays a field name.
75. As a person inspecting a document, I want a hit on a field name to be that field on each entry that has it, so that the column header is not a separate hit.
76. As a person inspecting a document, I want an entry that lacks the key not to hit that field name, so that a sparse list does not invent the field.
77. As a person inspecting a document, I want Field name to use the name as the table shows it, including `#value`, so that the limit matches the column.
78. As a person inspecting a document, I want an empty Field name to allow every field, so that the empty box does not mean "only the empty key".
79. As a person inspecting a document, I want the parent of `id` in `{"wrap": {"nums": [{"id": "a1"}]}}` to be `nums`, with no list index, so that the unnamed entry object is skipped.
80. As a person inspecting a document, I want the grandfather of that hit to be `wrap`, so that the filter climbs one named field and then stops.
81. As a person inspecting a document, I want a root object to have no parent, so that a parent requirement excludes hits that sit on the root.
82. As a person inspecting a document, I want a parent-attribute requirement to match nothing in a JSON document, so that `@` on a key is not an attribute.
83. As a person inspecting a document, I want two matches in one scalar to be two hits, so that a repeated word in one string is not collapsed.
84. As a person inspecting a document, I want the hit's character offset to count into the literal, including the quotes, so that the marked stretch matches the text that was tested.
85. As a person inspecting a document, I want Match case on by default, so that `TRUE` does not match `true`.
86. As a person inspecting a document, I want an empty Find what to produce no hits, so that name boxes alone do not list every field.
87. As a person inspecting a document, I want choosing a hit to expand the embedded tables on its path and scroll that cell into view, so that JSON hits open the same way XML hits do.
88. As a person inspecting a document, I want the hit's path in the slash form, so that a JSON hit reads `/#value[1]/#value` rather than an XPath.
89. As a person inspecting a document, I want the outline of a JSON object or array kept after it is collapsed, so that opening an entry does not read the other entries again.
90. As a person inspecting a document, I want scan fraction and remaining time while a JSON document is scanning, so that a large file shows the same progress as XML.
91. As a person inspecting a document, I want an XML document to keep today's table rules, so that JSON support does not move `@`, `#text`, namespaces, or the two-or-more list rule.

## Implementation Decisions

- One Viewer session opens a byte source and chooses a grammar once, from the first byte after a leading BOM and whitespace. The desktop open dialog may offer a `*.json` filter. That filter does not choose the grammar.
- The session's existing surface stays the contract: a record table, a list table, a path, a text search, expand and collapse, and a scan that can stop. Field kinds stay scalar, record, and list. No second session type and no editor API.
- A JSON object is a record unless it is a list entry. A list entry that is an object contributes columns and is not also a record table. A JSON array is a list at every length, including zero, from the moment it is recognized, and it does not demote.
- `#value` is the field name of a root scalar, a root array, and any list entry that is a string, number, true, false, null, or array. An object key of that same spelling is that same field.
- A scalar's shown text is its JSON literal: strings in double quotes with text decoded and with `"` and `\` rewritten as `\"` and `\\`; numbers, `true`, `false`, and `null` as in the file. The full value uses that same literal. A collapsed object is the two characters `{}`. A collapsed array is `Name[n]`. A missing key is a blank cell.
- The visible window and a search walk the literal without assembling the whole scalar into one string, the same constraint already used for large XML values.
- A repeated key, an ill-formed byte, invalid UTF-8, and trailing junk after a finished value stop the scan. Tables built before the stop stay, and the stop is marked. Empty input, whitespace only, a second value, and a first byte that starts neither grammar do not open: no tables.
- Path steps store the field name and, for a list entry, a 1-based index. Display joins those steps with `/`. Names are stored and written raw. The display string is not parsed back into steps.
- XML scanning, XML tables, and XML search stay as they are. A file whose first byte is `<` uses that existing XML behavior.

## Testing Decisions

- Test through one seam: open a byte source in the Viewer session, pump the scan, and observe the record table, the list table, the path, search hits, expand and collapse, and whether the file opened or the scan stopped. Assert what a person can read. Do not assert parser tokens, buffer layout, or private outlines.
- JSON and XML share that seam. A `.json` suffix is not part of the behavior under test. The open-dialog filter is not a second seam.
- Cover the external cases in the user stories: grammar from the first byte, the refusals that do not open, a stop that keeps the prefix, both root shapes, `#value`, mixed arrays, empty and single-element arrays, literal spelling, duplicate keys, raw names, and search limits including parent and grandfather.
- Prior art is the existing session tests that open XML bytes and read tables, lists, search hits, expand, and scan stop. Match that style. A large-document test belongs there only when it checks a story a small fixture cannot, such as a window over a long literal.

## Out of Scope

- Editing, saving, and any write-back of the document.
- NDJSON, JSON Lines, and more than one JSON value in a file that opens.
- JSON5, comments, trailing commas, `NaN`, `Infinity`, single-quoted keys, and `+1` as legal input.
- Changing XML list rules, `@`, `#text`, namespaces, entities, or the XML scan.
- A separate JSON window, a type-word prefix on values, zero-based indexes, or escaping field names in the path.
- Searching the collapsed labels `{}` and `Name[n]` as values.
- Letting the last repeated key win.
- A depth cap that XML does not already have.
- Using the file suffix as the grammar.

## Further Notes

- ADR-0029 through ADR-0043 record the JSON decisions. ADR-0002's wait is over. XML and JSON still do not share one table rule.
- `contains null` hits the string `"null"` because the tested text is the literal `"null"`. `equals null` does not.
- An empty Field name allows every field, so it cannot mean "only the blank header".
- `Name[0]` is the collapsed count of an empty list. It is not a path index. The first entry is `Name[1]`.
