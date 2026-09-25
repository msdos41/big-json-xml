# Vertical headers unless the children are a same-named list

A parent is not always a horizontal table. Two or more sibling elements with the same parent and the same name are a list table (field names across the top). Anything that is not such a list — the document element, a one-off child, a single opened entry — is a record table (field names down the side). A one-row horizontal table for a singleton was rejected: it wastes the screen and is not how the user reads a non-list node.

ADR-0003 still decides which siblings belong to a list (same parent and name, gaps allowed). A list field is shown by expanding its cell (ADR-0009), not by navigating away.
