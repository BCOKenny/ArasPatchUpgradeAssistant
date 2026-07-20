using ArasPatchUpgradeAssistant.Helpers;

namespace ArasPatchUpgradeAssistant.Tests.Helpers;

public sealed class PasswordMaskTests
{
    [Theory]
    [InlineData("")]
    [InlineData("innovator")]
    [InlineData("秘密")]
    public void Create_NeverReturnsPlainText(string password)
    {
        Assert.Equal(new string('*', password.Length), PasswordMask.Create(password));
    }
}
