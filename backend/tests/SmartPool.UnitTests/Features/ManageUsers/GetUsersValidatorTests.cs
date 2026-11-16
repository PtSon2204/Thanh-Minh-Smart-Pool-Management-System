using FluentValidation.TestHelper;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class GetUsersValidatorTests
{
    private readonly GetUsersValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Query mặc định → hợp lệ")]
    public async Task Validate_DefaultQuery_HasNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery())).ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID02 (A) - PageIndex bằng 0 → báo lỗi pageIndex")]
    public async Task Validate_ZeroPageIndex_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { PageIndex = 0 }))
            .ShouldHaveValidationErrorFor(x => x.PageIndex).WithErrorMessage("Chỉ số trang phải lớn hơn 0.");

    [Fact(DisplayName = "UTCID03 (A) - PageSize bằng 0 → báo lỗi pageSize")]
    public async Task Validate_ZeroPageSize_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { PageSize = 0 }))
            .ShouldHaveValidationErrorFor(x => x.PageSize).WithErrorMessage("Kích thước trang phải từ 1 đến 100.");

    [Fact(DisplayName = "UTCID04 (A) - PageSize bằng 101 → báo lỗi pageSize")]
    public async Task Validate_PageSizeAboveMaximum_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { PageSize = 101 }))
            .ShouldHaveValidationErrorFor(x => x.PageSize).WithErrorMessage("Kích thước trang phải từ 1 đến 100.");

    [Fact(DisplayName = "UTCID05 (B) - PageSize bằng 1 → hợp lệ")]
    public async Task Validate_MinimumPageSize_HasNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { PageSize = 1 })).ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID06 (B) - PageSize bằng 100 → hợp lệ")]
    public async Task Validate_MaximumPageSize_HasNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { PageSize = 100 })).ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID07 (B) - SearchTerm sau trim đúng 100 ký tự → hợp lệ")]
    public async Task Validate_SearchTermAtMaximum_HasNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { SearchTerm = " " + new string('a', 100) + " " }))
            .ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID08 (B) - SearchTerm sau trim 101 ký tự → báo lỗi")]
    public async Task Validate_SearchTermAboveMaximum_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { SearchTerm = new string('a', 101) }))
            .ShouldHaveValidationErrorFor(x => x.SearchTerm)
            .WithErrorMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.");

    [Fact(DisplayName = "UTCID09 (B) - Role chữ thường, có khoảng trắng → hợp lệ")]
    public async Task Validate_RoleCaseAndWhitespace_HasNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { Role = " coach " })).ShouldNotHaveAnyValidationErrors();

    [Fact(DisplayName = "UTCID10 (A) - Role không hỗ trợ → báo lỗi role")]
    public async Task Validate_UnsupportedRole_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { Role = "MANAGER" }))
            .ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Vai trò phải là CUSTOMER, LIFEGUARD, SALES, COACH hoặc ADMIN.");

    [Fact(DisplayName = "UTCID11 (B) - Status ACTIVE/LOCKED không phân biệt hoa thường → hợp lệ")]
    public async Task Validate_SupportedStatuses_HaveNoErrors()
    {
        (await _validator.TestValidateAsync(new GetUsersQuery { Status = " active " })).ShouldNotHaveAnyValidationErrors();
        (await _validator.TestValidateAsync(new GetUsersQuery { Status = "locked" })).ShouldNotHaveAnyValidationErrors();
    }

    [Fact(DisplayName = "UTCID12 (A) - Status INACTIVE → báo lỗi status")]
    public async Task Validate_InactiveStatus_HasError() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { Status = "INACTIVE" }))
            .ShouldHaveValidationErrorFor(x => x.Status).WithErrorMessage("Trạng thái phải là ACTIVE hoặc LOCKED.");

    [Fact(DisplayName = "UTCID13 (B) - Role và Status chỉ có khoảng trắng → hợp lệ")]
    public async Task Validate_WhitespaceFilters_HaveNoErrors() =>
        (await _validator.TestValidateAsync(new GetUsersQuery { Role = " ", Status = " " }))
            .ShouldNotHaveAnyValidationErrors();
}
