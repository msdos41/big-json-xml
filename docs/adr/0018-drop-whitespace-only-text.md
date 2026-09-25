# Whitespace-only text is not a field

Indentation and newlines between elements are character data in XML. In this viewer they are not fields and do not become `#text` rows. A text value is kept only when it contains a non-whitespace character, and then it is kept as written, including any spaces beside that character. `xml:space="preserve"` does not change this.

Keeping whitespace-only text was rejected because pretty-printed data files would show a blank `#text` row between every field. Honouring `xml:space` was rejected as a rule data files do not need.
