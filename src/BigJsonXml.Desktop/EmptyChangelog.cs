namespace BigJsonXml.Desktop;

public static class EmptyChangelog
{
    public static string Latest(string changelog) => "";

    public const string Text = """
        CHANGELOG

        v1
        --
        viewer. one xml or json document.
        the file on disk stays as it is.

        tables
          two or more same-named siblings are a list table
          a json array is a list table, of one value or of none
          one element, or one object, is a record table
          an attribute keeps the @ written in the file

        cell
          the next table is drawn inside the cell
          a long value wraps there when the cell is opened
          one vertical scrollbar, one horizontal scrollbar

        search
          names and values, including a table not expanded
          find next steps. find all lists. esc clears the mark

        scan
          one forward read. the bar is bytes reached over the length
          remaining time follows the pace so far
          the first byte after the bom and whitespace chooses
          "<" is xml. a json start byte is json

        left out
          no edit, no save. no external dtd, no external entity
          one json value, utf-8. no comment, no trailing comma

        keys
          ctrl+o  open    ctrl+f  find    esc  clear the mark
          double-click a column edge to fit it
        """;
}
