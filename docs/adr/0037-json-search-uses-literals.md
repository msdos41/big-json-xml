# JSON search compares the shown literal

A JSON value match tests the literal in the cell, the same spelling ADR-0035 shows. `equals null` hits null and not the string `"null"`. `contains null` hits both, because the quoted literal contains those letters. `{}` and a collapsed `Name[n]` are not values. Searching the decoded text, or every string on screen, was rejected.
