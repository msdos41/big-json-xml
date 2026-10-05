# A JSON list is an array

A JSON list is one array. Its values may be objects, scalars, null, arrays, or a mixture. An object cannot form a list by repeating a key. Grouping array values by whether they look alike is rejected, as it was for XML in ADR-0003. This supersedes ADR-0002's decision to wait. XML lists stay same-parent, same-name, and two or more. ADR-0031 decides how short that array may be.
