# 02: Show a JSON array of objects as a list table

**What to build:** A person can read a JSON array of objects as a list table. The array is a list at length zero and at length one. Columns are the objects' keys. A missing key is a blank cell. The list expands inside the cell, and the first entry is index 1.

**Blocked by:** 01: Open a JSON object as a record table

**Status:** ready-for-agent

- [ ] An array is a list field as soon as it is recognized, including `[]` and an array of one object. It does not wait for a second entry and it does not demote.
- [ ] Collapsed, the cell reads `Name[n]`. `n` grows as entries are seen and comes from the scan already walking the array.
- [ ] Object entries contribute their keys as columns, in the order first seen. A key seen later is added at the end.
- [ ] A list entry that is an object is not also drawn as a record table. Its keys are the columns of that row.
- [ ] A key absent from one entry is a blank cell, not `null` and not `""`.
- [ ] Expanding the list draws it inside the cell. Collapsing it and opening one entry again does not read the other entries again.
- [ ] The first entry's path index is 1. `Name[0]` is only the collapsed label of an empty list, not a path step. A field on the first object of `{"nums": [{"id": "a1"}]}` has a path `/nums[1]/id`.
- [ ] Viewer session tests cover an empty array, a single object, a longer array, a sparse key, the collapsed count, and the path index.
