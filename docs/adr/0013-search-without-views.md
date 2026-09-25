# Search is text or field, over the document, and a hit can be revealed

Search does not require tables to be expanded. v1 has two queries: text search (a character sequence in attribute values and character data) and field search (a named field and a value). It does not have XPath. Whether a field value must match exactly is not decided. A hit stores enough location to expand the ancestor cells and scroll the single outer scrollbar to that cell.

XPath was rejected because it needs a navigable index of every element. The original prompt used "XPath" for this whole-file search.
