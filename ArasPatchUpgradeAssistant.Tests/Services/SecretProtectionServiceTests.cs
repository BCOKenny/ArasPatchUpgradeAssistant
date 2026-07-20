using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class SecretProtectionServiceTests
{
    [Fact]
    public void Protect_ProducesBase64CipherTextThatCanBeUnprotected()
    {
        var service = new SecretProtectionService();

        var protectedText = service.Protect("innovator");
        var plainText = service.Unprotect(protectedText);

        Assert.NotEqual("innovator", protectedText);
        Assert.Equal("innovator", plainText);
        Assert.NotEmpty(Convert.FromBase64String(protectedText));
    }

    [Fact]
    public void Protect_BlankText_ReturnsBlankText()
    {
        var service = new SecretProtectionService();

        Assert.Equal(string.Empty, service.Protect(string.Empty));
        Assert.Equal(string.Empty, service.Unprotect(string.Empty));
    }
}
