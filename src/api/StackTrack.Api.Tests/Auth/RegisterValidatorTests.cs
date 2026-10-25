using FluentAssertions;
using StackTrack.Application.Auth.Register;

namespace StackTrack.Api.Tests.Auth;

public class RegisterValidatorTests
{
    static readonly RegisterCommandValidator Validator = new();

    static RegisterCommand Valid(
        string displayName = "Sara Ahmed",
        string email = "sara@example.com",
        string password = "Valid1234",
        string? confirm = null) =>
        new(displayName, email, password, confirm ?? password);

    [Theory]
    [InlineData("a", false)]
    [InlineData(" a ", false)]
    [InlineData(" ab ", true)]
    [InlineData("ab", true)]
    [InlineData("", false)]
    public void DisplayName_is_checked_after_trimming_lower_bound(string name, bool valid) =>
        Validator.Validate(Valid(displayName: name)).IsValid.Should().Be(valid);

    [Fact]
    public void DisplayName_50_chars_passes_and_51_fails()
    {
        Validator.Validate(Valid(displayName: new string('a', 50))).IsValid.Should().BeTrue();
        Validator.Validate(Valid(displayName: new string('a', 51))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DisplayName_padded_to_50_chars_passes_because_it_is_trimmed() =>
        Validator.Validate(Valid(displayName: " " + new string('a', 50) + " ")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    public void Email_must_be_an_address(string email) =>
        Validator.Validate(Valid(email: email)).Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact]
    public void Email_over_320_chars_fails() =>
        Validator.Validate(Valid(email: new string('a', 320) + "@x.com")).Errors
            .Should().Contain(e => e.PropertyName == "Email");

    [Theory]
    [InlineData("Short1", "At least 8 characters")]
    [InlineData("alllower1", "One uppercase letter")]
    [InlineData("ALLUPPER1", "One lowercase letter")]
    [InlineData("NoDigitsHere", "One digit")]
    public void Weak_password_names_the_unmet_rule(string password, string message) =>
        Validator.Validate(Valid(password: password)).Errors
            .Should().ContainSingle(e => e.PropertyName == "Password" && e.ErrorMessage == message);

    [Fact]
    public void Password_with_every_rule_unmet_reports_each_rule()
    {
        var messages = Validator.Validate(Valid(password: "")).Errors
            .Where(e => e.PropertyName == "Password").Select(e => e.ErrorMessage);

        messages.Should().Contain(["At least 8 characters", "One uppercase letter", "One lowercase letter", "One digit"]);
    }

    [Fact]
    public void Password_over_128_chars_fails() =>
        Validator.Validate(Valid(password: "Aa1" + new string('x', 126))).Errors
            .Should().Contain(e => e.PropertyName == "Password");

    [Fact]
    public void Password_of_exactly_128_chars_passes() =>
        Validator.Validate(Valid(password: "Aa1" + new string('x', 125))).IsValid.Should().BeTrue();

    [Fact]
    public void Valid_password_passes() =>
        Validator.Validate(Valid(password: "Valid1234")).IsValid.Should().BeTrue();

    [Fact]
    public void Confirm_password_must_match() =>
        Validator.Validate(Valid(confirm: "Different1")).Errors
            .Should().Contain(e => e.PropertyName == "ConfirmPassword");
}
