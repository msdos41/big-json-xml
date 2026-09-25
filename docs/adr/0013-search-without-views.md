# Search runs over the document, not over built views

Search must be able to find hits in parts of the document whose tables have not been expanded or built. The query language is not decided. XPath was the original prompt's name for this, and it is the expensive interpretation: a real XPath engine needs a navigable element index. Plain text search and field search do not.
