using ECafe.Application.Common.Errors;
using ECafe.Domain.Exceptions;
using Xunit;

namespace ECafe.Tests;

public sealed class ErrorMessageProviderTests
{
    private readonly ErrorMessageProvider _provider = new();

    [Fact]
    public void New_service_error_codes_have_a_message()
    {
        var codes = Enum.GetValues<ErrorCode>()
            .Where(code => (int)code is >= 1135 and <= 1144 or >= 6000 and < 8000);

        Assert.All(codes, code => Assert.NotEqual(code.ToString(), _provider.GetMessage(code)));
    }

    [Fact]
    public void Parameterized_message_includes_the_runtime_value()
    {
        var message = _provider.GetMessage(ErrorCode.TableNameAlreadyExists, new { name = "Masa-1" });

        Assert.Equal("'Masa-1' adlı masa artıq mövcuddur.", message);
    }
}
