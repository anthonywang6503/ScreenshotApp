using System.Reflection;
using System.Resources;

namespace ScreenshotApp.Tests;

public class AssemblyConfigurationTests
{
    [Fact]
    public void NeutralResourcesLanguage_IsEnglish()
    {
        var attribute = typeof(App).Assembly.GetCustomAttribute<NeutralResourcesLanguageAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("en", attribute.CultureName);
    }
}
