# XML table viewer

Status: resolved

## Problem Statement

A person has an XML document too large to open in a text editor or a DOM-based editor. They need to see its structure as tables, move through a very large list, and search the whole document before every nested table has been built. They must not change the document.

## Solution

A read-only desktop Viewer opens the document and shows it as record tables and list tables. The document element is a record table. Two or more same-named siblings are a list table, drawn inside the cell that was expanded. The document has one scrollable surface. Search runs over the document, not over tables already built, and choosing a hit expands the cells on its path and scrolls that cell into view.

## User Stories

1. As a person inspecting a document, I want to open an XML document without it being turned into a tree of objects, so that a GB-sized document stays usable.
2. As a person inspecting a document, I want the first view to appear before the whole file has been scanned, so that I am not waiting on the end of the file.
3. As a person inspecting a document, I want the document element to appear as a record table, so that a single root is read as fields down the side rather than as a one-row horizontal table.
4. As a person inspecting a document, I want field names down the side of a record table, so that one element's attributes and children read like a form.
5. As a person inspecting a document, I want two or more sibling elements with the same parent and the same name to appear as a list table, so that repeated elements read as rows.
6. As a person inspecting a document, I want those siblings to stay one list even when another element sits between them, so that an occasional different tag does not shatter the list.
7. As a person inspecting a document, I want a name that occurs once to stay on the record table, so that a singleton is never a one-row horizontal table.
8. As a person inspecting a document, I want a name seen once to become a list as soon as the second sibling is seen, so that the table corrects itself during the scan.
9. As a person inspecting a document, I want that promotion to stick, so that the table does not flip back to a record field if later scanning is still in progress.
10. As a person inspecting a document, I want fields discovered later to be added after the fields already shown, so that early rows stay put while the scan continues.
11. As a person inspecting a document, I want a collapsed list field to read `Name[n]`, so that I can see how large the list is without expanding it.
12. As a person inspecting a document, I want `n` to grow as siblings are seen and become exact when that parent's children have all been seen, so that the count does not require a second pass.
13. As a person inspecting a document, I want a child that occurs once and has no fields of its own to show its text directly, so that simple values do not need an expand control.
14. As a person inspecting a document, I want an expand control on a list field, so that I can open that list inside the cell.
15. As a person inspecting a document, I want an expand control on a child that occurs once and itself has fields, so that I can open that child as a record table inside the cell.
16. As a person inspecting a document, I want the list table to be drawn inside the cell I expanded, so that I do not leave the current table for another window.
17. As a person inspecting a document, I want the row that owns the expanded cell to grow, so that the embedded table has room.
18. As a person inspecting a document, I want several cells to be expandable at the same time, so that I can keep more than one nested table open.
19. As a person inspecting a document, I want collapsing a cell to return that row to a single line, so that I can put the nested table away.
20. As a person inspecting a document, I want a list entry's fields to be the columns of that row, so that I do not also see the same fields repeated as a vertical table inside the row.
21. As a person inspecting a document, I want attribute names written with a leading `@`, so that `@id` and a child element `id` are obviously different fields.
22. As a person inspecting a document, I want a namespaced name to show the prefix written in the file, so that the table matches the document I opened.
23. As a person inspecting a document, I want two spellings of the same namespace URI and local name to be the same field, so that a prefix change does not split one column into two.
24. As a person inspecting a document, I want an element's own non-whitespace text to appear as `#text` when that element also has attributes or element children, so that mixed content is visible.
25. As a person inspecting a document, I want indentation and blank lines between elements to be ignored, so that a pretty-printed file is not a stack of empty `#text` rows.
26. As a person inspecting a document, I want `xml:space="preserve"` not to bring those blank lines back, so that data files stay compact.
27. As a person inspecting a document, I want spaces that sit beside real text to be kept, so that significant padding inside a value is not stripped.
28. As a person inspecting a document, I want comments and processing instructions to be absent, so that the tables show elements, attributes, and text only.
29. As a person inspecting a document, I want internal entities expanded in the text I see, so that a value matches what the XML declares inside the document.
30. As a person inspecting a document, I want the viewer to refuse external entities, DTD fetches, and network access, so that opening an untrusted file cannot read other files or the network.
31. As a person inspecting a document, I want a well-formedness error to stop the scan and leave the tables already shown, so that I can still read the valid prefix.
32. As a person inspecting a document, I want that error marked at the point the scan stopped, so that I know the tables do not cover the rest of the file.
33. As a person inspecting a document, I want the viewer not to guess past a well-formedness error, so that later rows are not invented.
34. As a person inspecting a document, I want one scrollable surface for the whole document, vertically and horizontally, so that nested tables do not each bring their own scrollbar.
35. As a person inspecting a document, I want only the cells in view to be drawn, so that an expanded list of a million entries does not create a million controls.
36. As a person inspecting a document, I want to jump to an arbitrary entry of an expanded list, so that the scrollbar is usable on a long list.
37. As a person inspecting a document, I want that jump not to build an object for every entry before the one I asked for, so that visiting the end of a list stays cheap.
38. As a person inspecting a document, I want the list's scroll range to grow while its entries are still being found, so that I can read the first rows before the list scan finishes.
39. As a person inspecting a document, I want a collapsed list not to keep its offset index, so that closing a large list releases that memory.
40. As a person inspecting a document, I want a scalar cell to show at most 200 characters, so that a huge text node does not get decoded into one string.
41. As a person inspecting a document, I want that window to be the start of the value when I have not come from a hit, so that the preview is stable.
42. As a person inspecting a document, I want an expand control on a truncated scalar, so that I can ask to see the whole value.
43. As a person inspecting a document, I want the full value to grow the same row on the same scroll surface, so that reading the rest does not open another window or another scrollbar.
44. As a person inspecting a document, I want only the visible slice of that full value to be decoded, so that showing the whole text does not allocate the whole text.
45. As a person inspecting a document, I want collapsing the full value to restore the 200-character window, so that the row can become short again.
46. As a person inspecting a document, I want opening a list not to expand every long text inside it, so that full values stay opt-in.
47. As a person inspecting a document, I want to search the whole document for a character sequence, so that I can find text in tables I have not expanded.
48. As a person inspecting a document, I want that text search to look only at attribute values and character data, so that tag names are not false hits.
49. As a person inspecting a document, I want text search to be case-sensitive, so that XML values are not folded.
50. As a person inspecting a document, I want a query with no `=` and no `~` to be a text search, so that typing a word searches values.
51. As a person inspecting a document, I want `name = value` to match a field only when the whole value is equal, so that `@id = 2` does not match `20`.
52. As a person inspecting a document, I want `name ~ value` to match a field whose value contains the sequence, so that I can search inside a long text field.
53. As a person inspecting a document, I want field search to be case-sensitive for both the name and the value, so that `@id` and `@Id` stay distinct.
54. As a person inspecting a document, I want field search to use the same spelling I see in the table, including `@` and prefixes, so that I can copy a field name into the query.
55. As a person inspecting a document, I want hits to appear while the scan is still running, so that I do not wait for the end of a GB file.
56. As a person inspecting a document, I want hits in document order, so that I can move through the file from top to bottom.
57. As a person inspecting a document, I want each hit to remember its path from the document element, so that the viewer can open it later without a full-document index.
58. As a person inspecting a document, I want choosing a hit to expand every embedded table on that path, so that the matching cell becomes visible even if I had not opened those lists.
59. As a person inspecting a document, I want choosing a hit to scroll the single surface so that cell is in view and marked, so that I can see which match I picked.
60. As a person inspecting a document, I want a hit inside a long scalar to show a 200-character window around the match, so that I see the words that matched rather than only the start of the value.
61. As a person inspecting a document, I want revealing a hit not to expand the full value by itself, so that a hit in a huge text does not suddenly make the row as tall as the whole text.
62. As a person inspecting a document, I want search to keep working after I collapse a list, so that search is not tied to tables I currently have open.
63. As a person inspecting a document, I want an empty element to still appear as a field or an entry, so that a present-but-empty tag is not dropped.
64. As a person inspecting a document, I want the viewer not to edit or save the document, so that opening a file cannot change it.
65. As a person inspecting a document, I want to run the viewer as one Windows executable, so that I do not install a separate runtime.

## Implementation Decisions

- The product is one Viewer session plus a thin desktop shell. The session is the behavior boundary. The shell draws whatever the session says is visible and forwards expand, collapse, scroll, search, and reveal. It does not own table rules.
- v1 targets .NET 10 and an Avalonia shell using MVVM. The table is a custom virtualized surface, not TreeDataGrid. Native AOT is not required. The shipping build is a self-contained single-file executable for Windows. The session and scanner must not call Win32 APIs. macOS and Linux are not built or tested for v1.
- The session reads the document incrementally. A presentation is available before the scan reaches end of file. Tests can supply the bytes in chunks and observe the presentation between chunks.
- A record table's fields are that one element's attributes, then its element children and `#text`, in document order, with the promotion rule below. A list table's columns are the union of its entries' fields. Columns already present keep their order; a newly discovered field is appended.
- A child name that has been seen once is a field of the parent record. The second element with that same parent and name replaces that field with a list field. It never turns back into a plain field. Different-named siblings do not join the list and do not split it.
- A list field's collapsed label is `Name[n]`, where `Name` is the field's displayed name and `n` is how many siblings of that name have been seen. `n` comes from the scan that is already walking those siblings. When that scan finishes, `n` is exact.
- Expanding a list field embeds a list table in that cell and grows the row. Expanding a once-only child that has fields of its own embeds a record table in that cell. A once-only child with only scalar character data shows that text and has no expand control. Several cells may be expanded at once. Collapse removes the embedded table and restores the collapsed label or the scalar window.
- The document has one scrollable surface, vertical and horizontal. Embedded tables and full values do not add scroll surfaces. Only the visible slice of an embedded list or a full value is realized.
- An expanded list may build an offset index of its own entries so an arbitrary entry can be read without constructing entries before it. That index is created while the list is scanned, the scroll range grows as offsets are found, and the index may be discarded when the cell collapses. No other offset index is kept. Search does not read it.
- A scalar value is expanded character data. Whitespace-only character data is omitted, including under `xml:space="preserve"`. Character data that contains a non-whitespace character is kept as written, including spaces beside that character. If an element has several such pieces, they are one `#text` value concatenated in document order, placed at the first piece. An element that exists but has no remaining character data still appears; its scalar window is empty.
- Comments and processing instructions are skipped. They are not fields and not errors.
- Internal entities are expanded before text is shown or searched. External entities, external DTD subsets, and any network fetch are well-formedness errors. The scan stops. The session reports the error and keeps the presentation built so far. It does not skip the bad region or invent later entries.
- A scalar cell shows at most 200 characters. Normally that window is the start of the value. The user can expand that cell to the full value: the row grows to the virtual height of the text on the one scroll surface, and only the slice in view is decoded. Collapse restores the 200-character window. Expanding a list does not expand full values inside it.
- Search reads the document, not built tables. Hits are reported as they are found, in document order. A query is case-sensitive. If it contains neither `=` nor `~`, it is a text search over attribute values and character data, not over names. Otherwise the first `=` or `~` is the operator: `=` is whole-value equality, `~` is contains. The field name is the trimmed text before the operator and uses the displayed spelling (`@` for an attribute, prefixes as written). The value is the text after the operator with only the whitespace immediately following the operator removed; trailing spaces are significant. A prefixed name matches namespace URI plus local name, using prefixes declared in the document. An unprefixed element name matches an element in no namespace, or in the default namespace in scope at that element. `@` never matches an element.
- A hit stores a path and, when the match is inside a scalar, the character offset of the match in the expanded value. A path step is a displayed field name plus, for a list field, the 1-based position of that entry among siblings of the same name. Revealing a hit expands each embedded table on the path, scrolls the cell into view, and marks it. If the match is inside a scalar longer than 200 characters, the cell's window is centered on the match. Reveal does not expand the full value.
- Text search does not match collapsed labels such as `Order[1000000]`. Field search does not treat a list field as a scalar value.
- The session must not offer the whole document as a tree or as one string. Callers read a record, a slice of list entries, a scalar window, or a slice of a full value.
- Encoding follows the XML declaration. If it is absent, the encoding is UTF-8.

## Testing Decisions

- One seam: the Viewer session. Tests open XML bytes, expand and collapse cells, read a slice of a list, read a scalar window, expand a full value and read only a slice of it, run a search, and reveal a hit. They observe table kind, field names, `Name[n]`, windows, paths, the marked hit, and a stopped error.
- A good test asserts that external behavior. It does not assert mmap, struct layouts, or that an offset index exists. It does assert that reading entry N of a long list does not require an object per earlier entry, and that a search returns hits while the matching list is collapsed.
- Rule tests use small XML documents, including documents fed in chunks so promotion, `Name[n]`, and the stopped error can be seen mid-scan. One scale test uses a generated list large enough that a DOM or an object-per-entry approach would dominate, and reads a slice at the end.
- There is no existing test suite. The desktop shell is not a behavior seam. Single-file publish is a packaging check, not a session test.
- Do not add multi-GB fixtures.

## Out of Scope

- Editing, saving, and any piece-table or delta of the document.
- JSON.
- XPath, or any query other than text search and `=` / `~` field search.
- TreeDataGrid, and column virtualization aimed at millions of columns. A list is expected to have on the order of tens of columns.
- A full-document index of every element.
- Sorting, copy, and export.
- Repairing or skipping past a malformed region.
- Native AOT.
- Shipping or testing macOS and Linux builds.
- A second window, pane, or scrollbar for an embedded table or a full value.
- Auto-opening the single large list under the document element. The document element is always the first record table.
- Treating whitespace-only text as a field, even when `xml:space="preserve"` is set.

## Further Notes

- Glossary terms in `CONTEXT.md` are the names to use in code and tests. ADRs 0001 through 0019 record why. ADR-0004 and ADR-0006 are superseded and should not be implemented.
- `5GB` and `500MB` are not acceptance numbers. The acceptance stand-in is: the session never materializes the document as a tree, list slices stay proportional to the slice, and search works without those tables being expanded.
- A text query cannot search for the characters `=` or `~`, because those characters select field search. That is accepted.
