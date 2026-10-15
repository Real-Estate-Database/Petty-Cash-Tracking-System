namespace PettyCashDatabase.Web.Client.Tests.Components;

public sealed class WebClientAssemblyTests
{
    [Fact]
    public void WebClientAssembly_IsLoadable()
    {
        Assert.NotNull(typeof(PettyCashDatabase.Web.Client._Imports).Assembly);
    }
}
