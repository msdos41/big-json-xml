# JSON is UTF-8 and stops at the first ill-formed byte

A JSON document is UTF-8. A leading BOM is ignored. The grammar is one JSON value, without comments, trailing commas, or `NaN`. Empty text, whitespace only, or a second value does not open. Any other ill-formed byte stops the scan, and tables built before the stop stay. JSON5 was rejected. Refusing the whole file on a late syntax error was rejected because a large document is still readable up to the break.
