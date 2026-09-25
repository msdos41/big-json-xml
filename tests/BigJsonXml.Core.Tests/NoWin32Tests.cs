using System.Reflection;
using System.Runtime.InteropServices;

namespace BigJsonXml.Tests;

public class NoWin32Tests
{
    [Fact]
    public void Session_assembly_does_not_declare_win32_imports()
    {
        var assembly = typeof(ViewerSession).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                Assert.Null(method.GetCustomAttribute<DllImportAttribute>());
        }
    }
}