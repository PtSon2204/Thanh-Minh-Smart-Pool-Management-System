using FluentValidation.Results;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class ChangeUserStatusValidatorTests
{
    private readonly ChangeUserStatusValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Status ACTIVE → hợp lệ")]
    public async Task Validate_Active_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Command("ACTIVE"))).IsValid);

    [Fact(DisplayName = "UTCID02 (N) - Status LOCKED → hợp lệ")]
    public async Task Validate_Locked_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Command("LOCKED"))).IsValid);

    [Fact(DisplayName = "UTCID03 (B) - Status chữ thường, có khoảng trắng → hợp lệ")]
    public async Task Validate_CaseAndWhitespace_HasNoErrors() =>
        Assert.True((await _validator.ValidateAsync(Command(" locked "))).IsValid);

    [Fact(DisplayName = "UTCID04 (A) - Id rỗng → lỗi đúng PropertyName và message")]
    public async Task Validate_EmptyId_HasError() =>
        AssertError(await _validator.ValidateAsync(Command("LOCKED", Guid.Empty)), "Id", "Mã người dùng không hợp lệ.");

    [Fact(DisplayName = "UTCID05 (A) - Status rỗng → lỗi đúng PropertyName và message")]
    public async Task Validate_EmptyStatus_HasError() =>
        AssertError(await _validator.ValidateAsync(Command("")), "Status", "Trạng thái phải là ACTIVE hoặc LOCKED.");

    [Fact(DisplayName = "UTCID06 (A) - Status INACTIVE → lỗi validation")]
    public async Task Validate_Inactive_HasError() =>
        AssertError(await _validator.ValidateAsync(Command("INACTIVE")), "Status", "Trạng thái phải là ACTIVE hoặc LOCKED.");

    [Fact(DisplayName = "UTCID07 (A) - Status null → lỗi validation, không ném exception")]
    public async Task Validate_NullStatus_HasError() =>
        AssertError(await _validator.ValidateAsync(Command(null)), "Status", "Trạng thái phải là ACTIVE hoặc LOCKED.");

    private static ChangeUserStatusCommand Command(string? status, Guid? id = null) =>
        new() { Id = id ?? Guid.NewGuid(), Status = status! };

    private static void AssertError(ValidationResult result, string field, string message) =>
        Assert.Contains(result.Errors, error => error.PropertyName == field && error.ErrorMessage == message);
}
