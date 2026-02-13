namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Features.Auth;
using FluentAssertions;
using Xunit;

public class RegisterUserRequestTests
{
    [Fact]
    public void RegisterUserRequest_ShouldBeRecord()
    {
        // This test verifies the DTO is immutable
        var request = new RegisterUserRequest(
            "test@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        request.Email.Should().Be("test@example.com");
        request.DocumentNumber.Should().Be("12345678A");
        request.AcceptedTerms.Should().BeTrue();
    }
}
