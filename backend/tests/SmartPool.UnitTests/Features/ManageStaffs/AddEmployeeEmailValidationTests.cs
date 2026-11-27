using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class AddEmployeeEmailValidationTests
{
    [Theory]
    [InlineData("employee@smartpool.com")]
    [InlineData("employee@SMARTPOOL.COM")]
    [InlineData("Name <employee@example.test>")]
    public void InvalidPersonalEmailIsReportedOnPersonalEmail(string email)
    {
        var result = new AddEmployeeValidator(TimeProvider.System).Validate(new AddEmployeeCommand { PersonalEmail = email });
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AddEmployeeCommand.PersonalEmail));
    }
}
