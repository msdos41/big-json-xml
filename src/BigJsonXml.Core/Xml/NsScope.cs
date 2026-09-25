namespace BigJsonXml.Xml;

internal sealed class NsScope
{
    public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    public const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    public static NsScope Empty { get; } = Create();

    private readonly Dictionary<string, string> _prefixes;

    private NsScope(string defaultNamespace, Dictionary<string, string> prefixes)
    {
        Default = defaultNamespace;
        _prefixes = prefixes;
    }

    public string Default { get; }

    public static NsScope Create()
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["xml"] = XmlNamespace,
            ["xmlns"] = XmlnsNamespace,
        };
        return new NsScope("", prefixes);
    }

    public bool TryResolve(string prefix, out string uri) => _prefixes.TryGetValue(prefix, out uri!);

    public NsScope WithDefault(string uri) =>
        new(uri, new Dictionary<string, string>(_prefixes, StringComparer.Ordinal));

    public NsScope WithPrefix(string prefix, string uri)
    {
        var copy = new Dictionary<string, string>(_prefixes, StringComparer.Ordinal)
        {
            [prefix] = uri,
        };
        return new NsScope(Default, copy);
    }
}
