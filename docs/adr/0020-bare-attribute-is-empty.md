# A bare start-tag name is an attribute with an empty value

Some documents write a flag as a name with no `=`, for example `visible` beside `locked="false"`. That name is an attribute whose value is empty. The scan keeps going, so later elements still become fields. This narrows ADR 0017: a bare name is not a well-formedness stop.

Stopping at the missing `=` was rejected. The open element never closes, and every child after that point stays off the record table. Inventing a value such as `true` was rejected because those characters are not in the file. An unquoted value (`flag=yes`) is still a well-formedness error and still stops the scan.
