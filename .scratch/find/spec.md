# Find

Status: ready-for-agent

## Problem Statement

Search is a single query string and a hit list docked beside the document. `name = value` and `name ~ value` are the only way to limit a hit to a field. A person looking at the reference find window cannot say which names and values count, cannot step to the next hit, and cannot read a hit as the cell text plus a path. The document must stay a viewer: choosing a hit still expands along its path and scrolls the one document surface.

## Solution

Two modeless windows sit over the document surface. The find window edits one text search. The results window lists the hits of that search. The toolbar query and the docked hit list go away. Find what is the character sequence. Match case defaults to on. Element name, attribute name, and value default to on. Comments are not searched. Field name, parent element name, grandfather element name, and parent attribute name are optional exact names and follow Match case. Empty name boxes add no limit. An empty Find what has no hits.

The tested text contains Find what, starts with it, or equals it. Entire cell means the whole field text equals Find what, and Start with then adds nothing. There is no wildcard. Comparison walks the field text and does not assemble a multi-megabyte value into one string.

Each hit is one match, in document order, including while the scan is still running. The results window shows the matched field text and the path. The matched stretch is marked in that text. The results window is a table whose columns are headed Found and XPath. Dragging the boundary between them changes the Found column's width, and releasing that drag refits a long value to the column. Hits are listed under one group for the open document. The group row shows that document's path and the match count, and it expands and collapses. A later open document can be another group. Search still reads only the one open document. A short value is shown whole. A long value is shown as a window around the match, as wide as the results column, per ADR-0023. The path is the slash form in ADR-0028. Choosing a hit, or Find Next, reveals it as ADR-0013 describes: expand the embedded tables on that path and scroll the one document surface to that cell. The document cell is marked as a whole cell. Find does not edit the document, does not search another document, and does not replace text.

## User Stories

1. As a person inspecting a document, I want the find window and the results window to be separate from the document surface, so that later search controls can be added without taking space from the table.
2. As a person inspecting a document, I want both windows to stay open over the table, so that I can read a hit and the cell together.
3. As a person inspecting a document, I want Find what to be the character sequence, so that I do not type `name = value` or `name ~ value`.
4. As a person inspecting a document, I want an empty Find what to produce no hits, so that filled-in name boxes alone do not list every field.
5. As a person inspecting a document, I want Match case on by default, so that `World` does not match `world`.
6. As a person inspecting a document, I want turning Match case off to compare by ordinal case-folding, so that the same switch covers Find what and the name boxes.
7. As a person inspecting a document, I want element names, attribute names, and values included by default, so that a sequence is found in any of those three.
8. As a person inspecting a document, I want to turn each of those three off, so that a search for `id` can ignore values.
9. As a person inspecting a document, I want an attribute name tested with its leading `@`, so that Find what `id` matches `@id` and Find what `@id` matches the whole name when Entire cell is on.
10. As a person inspecting a document, I want a namespaced name tested with the prefix written in the file, so that the search uses the same spelling as the table.
11. As a person inspecting a document, I want comments left out, so that search stays inside the document the viewer presents.
12. As a person inspecting a document, I want the default match to be contains, so that `123` hits inside `RTM=123`.
13. As a person inspecting a document, I want Start with to mean the whole field text starts with Find what, so that a match in the middle does not count.
14. As a person inspecting a document, I want Entire cell to mean the whole field text equals Find what, so that `123` does not hit `RTM=123`.
15. As a person inspecting a document, I want Entire cell to ignore Start with, so that the stricter rule is the one that applies when both are on.
16. As a person inspecting a document, I want that comparison to use the whole field text, so that a collapsed cell's visible window does not hide a match past the edge.
17. As a person inspecting a document, I want a long value judged without building one string of the whole value, so that search stays within ADR-0023.
18. As a person inspecting a document, I want no wildcard language, so that `*` is an ordinary character.
19. As a person inspecting a document, I want an empty Field name to allow every field, so that I can search the document without naming a field.
20. As a person inspecting a document, I want Field name `Start_node` and Find what `123` to hit a simple `Start_node` whose text contains `123`, so that the old `Start_node ~ 123` still has a form.
21. As a person inspecting a document, I want Field name `#text` to hit character data of an element that also has an attribute or a child element, so that mixed content is not confused with a simple element.
22. As a person inspecting a document, I want Field name `Start_node` not to hit that `#text`, so that the field on the hit is the one I named.
23. As a person inspecting a document, I want Entire cell plus a field name to require the whole value to equal Find what, so that the old `name = value` still has a form.
24. As a person inspecting a document, I want a simple `Start_node` containing only `RTM=123` to have parent element name `Extended_covering`, so that the field itself is not treated as its own parent.
25. As a person inspecting a document, I want `#text` under `Start_node` to have parent element name `Start_node`, so that the element which owns the text is the parent.
26. As a person inspecting a document, I want grandfather element name to be exactly one step above that parent, so that a higher ancestor does not qualify.
27. As a person inspecting a document, I want a name box to match the displayed name without a list index, so that `Extended_covering` covers every entry of that name.
28. As a person inspecting a document, I want a hit that has no element at a required step to be excluded, so that a document-element hit does not satisfy a grandfather name.
29. As a person inspecting a document, I want parent attribute name `@id` to require that the parent element has `@id`, so that the attribute's value is a separate test.
30. As a person inspecting a document, I want parent attribute name `id` not to match `@id`, so that attribute spelling stays the table spelling.
31. As a person inspecting a document, I want one hit per match, so that two occurrences of `123` in one field are two rows with the same text and different marked stretches.
32. As a person inspecting a document, I want the results text for a value hit to be that value, with the matched stretch marked, so that `RTM=123` shows `123` marked.
33. As a person inspecting a document, I want the results text for a name hit to be that field name, with the matched stretch marked, so that a hit on `@id` shows the name rather than the attribute value.
34. As a person inspecting a document, I want a long value in the results to show only the window around the match that fits the column, so that the row does not hold a multi-megabyte string.
35. As a person inspecting a document, I want the path column headed XPath and written as `/DIH_XML_schema/Harness/Extended_covering[6]/Start_node/#text`, so that the heading matches the find-all reference and the line stays a Path.
36. As a person inspecting a document, I want an attribute path step to keep `@`, so that `/.../Extended_covering[6]/@id` matches the field name.
37. As a person inspecting a document, I want that path line to be display only, so that pasting it into Find what does not jump to the cell.
38. As a person inspecting a document, I want Find All to list hits as they are found and to show the count while the scan continues, so that I need not wait for the end of the file.
39. As a person inspecting a document, I want a new Find All to replace the previous hits, so that the list belongs to the search I just ran.
40. As a person inspecting a document, I want choosing a result row to expand along that path and scroll the document surface to that cell, so that the independent list does not change reveal.
41. As a person inspecting a document, I want the revealed document cell marked as a whole cell, so that the character mark stays in the results text.
42. As a person inspecting a document, I want a hit on a list entry's own element name to reveal that entry's row, so that a name which is not its own cell still has a place to land.
43. As a person inspecting a document, I want a hit on a record-table field name to reveal that field's row, so that the row header is the name I found.
44. As a person inspecting a document, I want Find Next to reveal the next hit in document order, so that I can move without using the list.
45. As a person inspecting a document, I want the first Find Next to reveal the first hit, so that I have a start when nothing is current.
46. As a person inspecting a document, I want Find Next on the last hit to stop, so that search does not wrap to the first hit.
47. As a person inspecting a document, I want Find Next to use hits already found and to keep the scan going until the next hit exists, so that the first match can appear before the file ends.
48. As a person inspecting a document, I want the results row for the hit last revealed to be the current row when that hit is listed, so that Find Next and the list stay on the same hit.
49. As a person inspecting a document, I want the Find what dropdown to list the sequences used while this document is open, newest first, up to 20, so that I can repeat one.
50. As a person inspecting a document, I want that list dropped when the document closes and never written to disk, so that history stays with this opening.
51. As a person inspecting a document, I want search limited to the one open document, so that there is no search across files.
52. As a person inspecting a document, I want no replace controls, so that the viewer still cannot change the document.
53. As a person inspecting a document, I want the results columns headed Found and XPath, so that both columns have names.
54. As a person inspecting a document, I want to drag the boundary between those columns, so that a long value or a long path can have more room.
55. As a person inspecting a document, I want hits grouped under the document path with a match count, and that group to expand and collapse, so that a later document can sit as another group.

## Implementation Decisions

- The viewer session takes one structured text search: Find what, whether Match case is on, which of element name, attribute name, and value are included, whether the tested text must contain Find what, start with it, or equal it, plus an optional field name, parent element name, grandfather element name, and parent attribute name. A query string is not parsed. `=` and `~` are not operators.
- Match case off uses ordinal case-folding for Find what and for every name constraint. There is one switch.
- A prefixed name is typed as the table shows it. The field's identity remains the namespace URI plus the local name.
- A hit still stores its path and the matched stretch. The session can render the path in the slash form from ADR-0028, and it can supply the field text for a result row: the whole value when it fits the requested width, otherwise a window around the stretch. The caller passes that width.
- Contains, starts-with, and equals walk the field text. They do not assemble a multi-megabyte value into one string.
- A collapsed list label such as `Name[n]` is not a value. An element-name hit on a list entry is that entry, not that label.
- The window model holds the find window, the results window, the current hit, and the Find what history. The document keeps one scroll surface. Choosing a hit or Find Next reveals through the existing path rule in ADR-0013. Find Next walks hits in document order and lets the scan continue until the next hit exists or the scan stops.
- History lives in memory for the open document, newest first, at most 20 sequences. Closing the document drops it. It is not written to disk.

## Testing Decisions

- Test the behaviour a person can observe. Do not test private scan steps or control trees.
- Two existing seams cover this work. The viewer session is where a text search reports hits, stretches, path steps, streaming order, and reveal. The window model is where the two windows, the result columns, Find Next, history, and the document mark are exercised. Do not add a parser seam or a pixel seam.
- Session tests are the prior art for search. Assertions that describe the retired query string are replaced as the structured search lands, inside the ticket that changes that behaviour, not as a separate rewrite.
- Window-model tests are the prior art for opening a document, listing hits, and revealing one. They gain the find window and the results window.

## Out of Scope

- A Selection limit on the find window. The document surface can already select a stretch of text in one cell. That selection does not narrow a text search, and this work does not remove it.
- A filter over the hits already listed.
- Wildcards or other pattern syntax.
- Replace, replace-all, and skip-non-editable.
- Search across more than the open document.
- Comments and processing instructions.
- Evaluating the path line as XPath.

## Further Notes

- ADR-0027 records the structured text search. ADR-0028 records the path spelling. ADR-0013 still records reveal, and still rejects XPath.
- The tickets under this spec are the implementation slices. Ticket 01 is the only one that can start immediately.
- The document surface already selects a stretch of text inside one cell. This spec does not change that selection.
