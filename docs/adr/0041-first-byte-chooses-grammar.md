# The first byte chooses XML or JSON

After a leading BOM and whitespace, `<` is XML and a byte that can start a JSON value is JSON. Any other first byte does not open. The file name does not choose the grammar, so a `.json` file that starts with `<` is still XML. Choosing by suffix, or by two separate open commands, was rejected.
