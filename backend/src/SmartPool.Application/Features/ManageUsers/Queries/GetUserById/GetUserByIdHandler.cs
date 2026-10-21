using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageUsers.Queries.GetUserById;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, IActionResult>
{
    private readonly IUserOperations _users;

    public GetUserByIdHandler(IUserOperations users)
    {
        _users = users;
    }

    public async Task<IActionResult> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400,
                Title = "Mã người dùng không hợp lệ."
            });

        var user = await _users.GetUserByIdAsync(request.Id, cancellationToken);
        return user is null
            ? new NotFoundObjectResult(new ProblemDetails { Status = 404, Title = "Không tìm thấy người dùng." })
            : new OkObjectResult(user);
    }
}
