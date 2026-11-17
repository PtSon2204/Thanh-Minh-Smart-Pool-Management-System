using FluentValidation.Results;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class GetSwimClassesValidatorTests
{
    private readonly GetSwimClassesValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Query mặc định → hợp lệ")]
    public void Validate_Default_HasNoErrors() => Assert.True(_validator.Validate(new GetSwimClassesQuery()).IsValid);

    [Fact(DisplayName = "UTCID02 (A) - PageIndex = 0 → lỗi PageIndex")]
    public void Validate_ZeroPageIndex_HasError() => AssertError(new() { PageIndex = 0 }, "PageIndex");

    [Fact(DisplayName = "UTCID03 (B) - PageIndex đúng giới hạn → hợp lệ")]
    public void Validate_MaximumPageIndex_HasNoErrors() =>
        Assert.True(_validator.Validate(new GetSwimClassesQuery { PageIndex = int.MaxValue / 100 }).IsValid);

    [Fact(DisplayName = "UTCID04 (B) - PageIndex vượt giới hạn → lỗi PageIndex")]
    public void Validate_PageIndexAboveMaximum_HasError() =>
        AssertError(new() { PageIndex = int.MaxValue / 100 + 1 }, "PageIndex");

    [Fact(DisplayName = "UTCID05 (A) - PageSize = 0 → lỗi PageSize")]
    public void Validate_ZeroPageSize_HasError() => AssertError(new() { PageSize = 0 }, "PageSize");

    [Fact(DisplayName = "UTCID06 (B) - PageSize = 100 → hợp lệ")]
    public void Validate_MaximumPageSize_HasNoErrors() =>
        Assert.True(_validator.Validate(new GetSwimClassesQuery { PageSize = 100 }).IsValid);

    [Fact(DisplayName = "UTCID07 (B) - PageSize = 101 → lỗi PageSize")]
    public void Validate_PageSizeAboveMaximum_HasError() => AssertError(new() { PageSize = 101 }, "PageSize");

    [Fact(DisplayName = "UTCID08 (B) - SearchTerm dài 150 ký tự → hợp lệ")]
    public void Validate_SearchAtMaximum_HasNoErrors() =>
        Assert.True(_validator.Validate(new GetSwimClassesQuery { SearchTerm = new string('a', 150) }).IsValid);

    [Fact(DisplayName = "UTCID09 (B) - SearchTerm dài 151 ký tự → lỗi SearchTerm")]
    public void Validate_SearchAboveMaximum_HasError() =>
        AssertError(new() { SearchTerm = new string('a', 151) }, "SearchTerm");

    [Fact(DisplayName = "UTCID10 (B) - Status dài 30 ký tự → hợp lệ")]
    public void Validate_StatusAtMaximum_HasNoErrors() =>
        Assert.True(_validator.Validate(new GetSwimClassesQuery { Status = new string('a', 30) }).IsValid);

    [Fact(DisplayName = "UTCID11 (B) - Status dài 31 ký tự → lỗi Status")]
    public void Validate_StatusAboveMaximum_HasError() =>
        AssertError(new() { Status = new string('a', 31) }, "Status");

    [Fact(DisplayName = "UTCID12 (B) - Scope customer/coach và khoảng trắng → hợp lệ")]
    public void Validate_AllowedScopes_HaveNoErrors()
    {
        foreach (var scope in new[] { "customer", " COACH ", "   " })
            Assert.True(_validator.Validate(new GetSwimClassesQuery { Scope = scope }).IsValid);
    }

    [Fact(DisplayName = "UTCID13 (A) - Scope khác customer/coach → lỗi đúng message")]
    public void Validate_UnsupportedScope_HasError()
    {
        var result = _validator.Validate(new GetSwimClassesQuery { Scope = "ADMIN" });

        Assert.Contains(result.Errors, x => x.PropertyName == "Scope" && x.ErrorMessage == "Phạm vi xem lớp không hợp lệ.");
    }

    private void AssertError(GetSwimClassesQuery query, string property)
    {
        ValidationResult result = _validator.Validate(query);
        Assert.Contains(result.Errors, x => x.PropertyName == property);
    }
}
