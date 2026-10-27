# BigJsonXml

A tool for reading and searching a very large XML or JSON file.

A release is one file. There is no installer and no separate runtime to set up. Download the [latest release](https://github.com/msdos41/big-json-xml/releases/latest).

<!-- When a short GIF exists, show it here:
<p align="center">
  <img src="docs/media/overview.gif" alt="Orders open as a table, with one cell expanded into another table" width="880">
</p>
-->

## What you get

- **Memory stays low on a large file.** A 1 GB file stays around 40 MB.
- **Reading does not block the window.** You can scroll and search while the file is still being read.

## How a file becomes a table

```xml
<orders>
  <order id="1001" status="open">
    <customer>Ada</customer>
    <city>London</city>
  </order>
  <order id="1002" status="shipped">
    <customer>Grace</customer>
    <city>New York</city>
  </order>
</orders>
```

The document opens on `orders`. Its only field is `order`, collapsed as `order[2]`. Expand that cell and the two orders are a table. The columns follow the file: `@id`, `@status`, `customer`, `city`.

```json
{
  "orders": [
    { "id": 1001, "customer": "Ada", "city": "London" },
    { "id": 1002, "customer": "Grace", "city": "New York" }
  ]
}
```

The same shape. The root object is a sheet, `orders` reads `orders[2]`, and expanding it gives the columns `id`, `customer`, `city`. JSON values are shown as literals: `1001` has no quotes, and `"Ada"` keeps its quotes. A file whose root is an array uses the field name `#value`.

The extension is a hint for the file picker. The format comes from the first byte after a leading BOM and whitespace: `<` is XML, and a byte that can start a JSON value is JSON.

Each document you open gets a tab. Expand a cell and the nested sheet or table is drawn there. The document has one vertical scrollbar and one horizontal scrollbar. Collapsed, a long value shows its start, cut to the cell width. Expanded, the full text wraps and the row grows. Double-click the right edge of a column to fit its width. Drag that edge to set the width yourself.

## Search

Press `Ctrl+F`, or the Find button. Names and values are searched even inside tables you have not expanded. A hit opens the cells on the way and scrolls that cell into view.

**Find what** is the character sequence to look for. An empty box has no hits. **Field name**, **Parent element name**, **Grandfather element name**, and **Parent attribute name** are optional limits. An empty limit matches every field. A parent attribute limit checks that the parent has that attribute; the attribute's value is not compared.

**Match case** is on when the window opens. **Element name**, **Attribute name**, and **Value** choose where the text is allowed to match, and each one can be turned off. Leave **Start with** and **Entire cell** off to match anywhere in the text. **Start with** keeps a hit that begins with the text. **Entire cell** keeps a hit that is the whole text.

**Find Next** moves to the next hit. **Find All** lists them. `Esc` clears the current mark.

## This release

This version reads a document and searches it.

JSON is one UTF-8 value: an object, an array, or a scalar. Comments, trailing commas, and a second value are outside that grammar. An ill-formed byte stops the read, and the tables built before that byte stay.

External DTDs and external entities are not read. The viewer does not open a network connection to expand them. Indentation and blank lines between XML elements are not fields.

## Roadmap

- [x] Read and search
- [ ] Edit and save
- [ ] Linux and macOS

## Build

Building and testing require the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Publish the single-file executable:

```powershell
dotnet publish src/BigJsonXml.Desktop/BigJsonXml.Desktop.csproj -c Release
```

The file is `src/BigJsonXml.Desktop/bin/Release/net10.0/win-x64/publish/BigJsonXml.exe`.

Run the tests from the repository root:

```powershell
dotnet test BigJsonXml.slnx
```

The desktop window is Avalonia. The reader and the tables are in `src/BigJsonXml.Core`.

## Release

The tag and asset rules are in [docs/releasing.md](docs/releasing.md).

## License

BigJsonXml is released under the [MIT License](LICENSE).
