using FluentAssertions;
using SchoolManagement.Domain.Common;
using Xunit;

namespace SchoolManagement.UnitTests;

public class ResultTests
{
    [Fact]
    public void SuccessResult_ShouldHaveIsSuccessTrueAndNoError()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void FailureResult_ShouldHaveIsSuccessFalseAndExpectedError()
    {
        // Arrange
        var error = Error.Validation("User.InvalidEmail", "The email format is invalid.");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void GenericResult_Success_ShouldReturnValue()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericResult_Failure_AccessingValueShouldThrow()
    {
        // Arrange
        var result = Result.Failure<string>(Error.NotFound("User.NotFound", "User not found."));

        // Act
        Action act = () => _ = result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
