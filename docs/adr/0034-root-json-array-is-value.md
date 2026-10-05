# The root JSON array is #value

A root array is a list field named `#value`, the same name as any other value that is not an object key. `[1, {"a": 2}]` is `/#value[1]/#value` and `/#value[2]/a`. A nameless path step and a second name such as `#root` were rejected. This closes the root-array question left by ADR-0033.
