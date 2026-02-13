namespace Abuvi.Tests.Unit.Features.Auth;

using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Models;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

/// <summary>
/// Unit tests for Auth registration endpoints
/// Tests the endpoint handler logic by calling internal methods directly
/// </summary>
public class AuthEndpointsTests_Registration
{
    private readonly IAuthService _authService;

    public AuthEndpointsTests_Registration()
    {
        _authService = Substitute.For<IAuthService>();
    }

    [Fact]
    public async Task RegisterUser_WithValidRequest_ReturnsOkWithUserResponse()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            "+34612345678",
            true
        );

        var expectedResponse = new UserResponse(
            Guid.NewGuid(),
            "user@example.com",
            "John",
            "Doe",
            "+34612345678",
            UserRole.Member,
            false,
            false,
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        _authService
            .RegisterUserAsync(request, Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await AuthEndpoints.RegisterUser(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Ok<ApiResponse<UserResponse>>>();
        var okResult = (Ok<ApiResponse<UserResponse>>)result;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Success.Should().BeTrue();
        okResult.Value.Data.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "existing@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        _authService
            .RegisterUserAsync(request, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("An account with this email already exists"));

        // Act
        var result = await AuthEndpoints.RegisterUser(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<UserResponse>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<UserResponse>>)result;
        jsonResult.StatusCode.Should().Be(400);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Code.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateDocumentNumber_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterUserRequest(
            "user@example.com",
            "Password123!",
            "John",
            "Doe",
            "12345678A",
            null,
            true
        );

        _authService
            .RegisterUserAsync(request, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("An account with this document number already exists"));

        // Act
        var result = await AuthEndpoints.RegisterUser(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<UserResponse>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<UserResponse>>)result;
        jsonResult.StatusCode.Should().Be(400);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Message.Should().Contain("document number");
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new VerifyEmailRequest("valid-token-123");

        _authService
            .VerifyEmailAsync(request.Token, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await AuthEndpoints.VerifyEmail(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Ok<ApiResponse<object>>>();
        var okResult = (Ok<ApiResponse<object>>)result;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new VerifyEmailRequest("expired-token");

        _authService
            .VerifyEmailAsync(request.Token, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("Verification token has expired"));

        // Act
        var result = await AuthEndpoints.VerifyEmail(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<object>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<object>>)result;
        jsonResult.StatusCode.Should().Be(400);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsNotFound()
    {
        // Arrange
        var request = new VerifyEmailRequest("invalid-token");

        _authService
            .VerifyEmailAsync(request.Token, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("User", Guid.Empty));

        // Act
        var result = await AuthEndpoints.VerifyEmail(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<object>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<object>>)result;
        jsonResult.StatusCode.Should().Be(404);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task ResendVerification_WithValidEmail_ReturnsOk()
    {
        // Arrange
        var request = new ResendVerificationRequest("user@example.com");

        _authService
            .ResendVerificationAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await AuthEndpoints.ResendVerification(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Ok<ApiResponse<object>>>();
        var okResult = (Ok<ApiResponse<object>>)result;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ResendVerification_WithAlreadyVerifiedEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResendVerificationRequest("verified@example.com");

        _authService
            .ResendVerificationAsync(request.Email, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException("Email is already verified"));

        // Act
        var result = await AuthEndpoints.ResendVerification(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<object>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<object>>)result;
        jsonResult.StatusCode.Should().Be(400);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Message.Should().Contain("already verified");
    }

    [Fact]
    public async Task ResendVerification_WithNonExistentEmail_ReturnsNotFound()
    {
        // Arrange
        var request = new ResendVerificationRequest("nonexistent@example.com");

        _authService
            .ResendVerificationAsync(request.Email, Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("User", Guid.Empty));

        // Act
        var result = await AuthEndpoints.ResendVerification(request, _authService, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonHttpResult<ApiResponse<object>>>();
        var jsonResult = (JsonHttpResult<ApiResponse<object>>)result;
        jsonResult.StatusCode.Should().Be(404);
        jsonResult.Value.Should().NotBeNull();
        jsonResult.Value!.Success.Should().BeFalse();
        jsonResult.Value.Error.Should().NotBeNull();
        jsonResult.Value.Error!.Code.Should().Be("NOT_FOUND");
    }
}
