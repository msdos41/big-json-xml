# A second JSON value does not open

`{}{}` is a second value, so the file does not open. `{} oops` is not a second value: the scan stops at the junk and the finished value stays. Treating every trailing byte as a refused file, or keeping the first of two values, was rejected.
