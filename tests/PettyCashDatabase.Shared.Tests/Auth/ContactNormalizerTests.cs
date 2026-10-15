using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Shared.Tests.Auth;

public sealed class ContactNormalizerTests
{
    [Fact]
    public void NormalizeEmail_TrimsAndUsesLowerCase()
    {
        Assert.Equal("admin@example.com", ContactNormalizer.NormalizeEmail(" Admin@Example.COM "));
    }

    [Fact]
    public void NormalizeTelephone_AppliesUgandaDefaultCountryCode()
    {
        Assert.Equal("+256701234567", ContactNormalizer.NormalizeTelephone("0701 234 567"));
    }
}
