using FluentValidation;

namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

public sealed class GetClassRegistrantContactValidator : AbstractValidator<GetClassRegistrantContactQuery>
{
    public GetClassRegistrantContactValidator()
    {
        RuleFor(query => query.ClassId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}
