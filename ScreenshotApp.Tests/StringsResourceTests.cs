using System.Text.RegularExpressions;
using System.Xml.Linq;
using ScreenshotApp.Resources;
using ScreenshotApp.Tests.TestSupport;

namespace ScreenshotApp.Tests;

/// <summary>介面文字集中於 Strings.resx：引用的鍵必須存在、資源不得閒置、XAML 不得寫死介面文字。</summary>
public partial class StringsResourceTests
{
    private static readonly string[] UiTextAttributes =
    [
        "Content", "Text", "Title", "ToolTip", "Header", "AutomationProperties.Name", "AutomationProperties.HelpText",
    ];

    [Fact]
    public void Get_MissingKey_ReturnsMarkedKey()
    {
        Assert.Equal("!NoSuchKey!", Strings.Get("NoSuchKey"));
    }

    [Fact]
    public void Get_ExistingKey_ReturnsTraditionalChineseText()
    {
        Assert.Equal("結束(&X)", Strings.Get("Tray_Exit"));
    }

    [Fact]
    public void Format_InsertsArguments()
    {
        Assert.Equal("目前已啟用：Ctrl+Shift+2", Strings.Format("Settings_HotkeyActive", "Ctrl+Shift+2"));
    }

    [Fact]
    public void EveryReferencedKey_ExistsInResources()
    {
        var referenced = ReferencedKeys();

        Assert.NotEmpty(referenced);
        Assert.Empty(referenced.Except(ResourceKeys()).Order());
        Assert.DoesNotContain(referenced, key => Strings.Get(key) == $"!{key}!");
    }

    [Fact]
    public void EveryResourceKey_IsReferenced()
    {
        Assert.Empty(ResourceKeys().Except(ReferencedKeys()).Order());
    }

    [Fact]
    public void EveryResourceValue_IsNotBlank()
    {
        Assert.DoesNotContain(ResourceKeys(), key => string.IsNullOrWhiteSpace(Strings.Get(key)));
    }

    [Fact]
    public void Xaml_HasNoHardcodedUserInterfaceText()
    {
        var violations = new List<string>();
        foreach (var path in RepositoryPaths.AppSourceFiles(".xaml"))
        {
            var document = XDocument.Load(path);
            foreach (var element in document.Descendants())
            {
                violations.AddRange(element.Attributes()
                    .Where(attribute => UiTextAttributes.Contains(attribute.Name.LocalName)
                        && attribute.Value.Length > 0
                        && !attribute.Value.StartsWith('{'))
                    .Select(attribute => $"{Path.GetFileName(path)}: {element.Name.LocalName}.{attribute.Name.LocalName}=\"{attribute.Value}\""));

                violations.AddRange(element.Nodes()
                    .OfType<XText>()
                    .Where(text => !string.IsNullOrWhiteSpace(text.Value))
                    .Select(text => $"{Path.GetFileName(path)}: {element.Name.LocalName} 內含文字 \"{text.Value.Trim()}\""));
            }
        }

        Assert.Empty(violations);
    }

    private static HashSet<string> ResourceKeys()
    {
        var resx = XDocument.Load(Path.Combine(RepositoryPaths.AppProject, "Resources", "Strings.resx"));
        return resx.Root!.Elements("data").Select(data => (string)data.Attribute("name")!).ToHashSet();
    }

    private static HashSet<string> ReferencedKeys()
    {
        var keys = new HashSet<string>();
        foreach (var path in RepositoryPaths.AppSourceFiles(".xaml"))
        {
            keys.UnionWith(XamlKeyPattern().Matches(File.ReadAllText(path)).Select(match => match.Groups["key"].Value));
        }

        foreach (var path in RepositoryPaths.AppSourceFiles(".cs"))
        {
            keys.UnionWith(CodeKeyPattern().Matches(File.ReadAllText(path)).Select(match => match.Groups["key"].Value));
        }

        return keys;
    }

    [GeneratedRegex(@"\{res:Str\s+(?<key>[A-Za-z0-9_]+)\s*\}")]
    private static partial Regex XamlKeyPattern();

    [GeneratedRegex(@"Strings\.(?:Get|Format)\(\s*""(?<key>[A-Za-z0-9_]+)""")]
    private static partial Regex CodeKeyPattern();
}
