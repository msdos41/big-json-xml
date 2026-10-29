# A search that ignores case also jumps between candidates

This amends ADR 0053. Its jump between candidates also applies when Match case is off, for the same needles: printable ASCII, with the same stops at JSON escapes and at XML entity references, CDATA sections, namespace declarations, and tags that could split the needle's text. When case is ignored, no character outside ASCII equals an ASCII character, so a hit's raw bytes still hold the needle with only its letters' case changed. The byte search compares the needle's first and last bytes sixteen at a time, folding letters, and checks the rest at each place both match. A needle with no letters is searched as it is. Hits are still found and checked by the same matcher as before.

The jump no longer needs JSON's item reader, so a JSON search with Starts with, Entire cell, or a name filter jumps too. Those conditions only narrow which places count as hits, so a place with no candidate still holds none.

A needle outside printable ASCII still parses every entry.
