using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.ManageServices.Commands;
using SmartPool.Application.Features.ManageServices.Contracts;
using SmartPool.Application.Features.ManageServices.Queries;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/v1/rentals")]
public sealed class RentalsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageDto<RentalDto>>> GetRentals([FromQuery] RentalListQuery query, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new GetRentalsQuery(query), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RentalCheckoutDto>> Checkout([FromBody] RentalCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetOperatorId(out var operatorId)) return Forbid();
        var result = await sender.Send(new CheckoutRentalCommand(request, operatorId), cancellationToken);
        return result.IsSuccess ? Created($"/api/v1/rentals/{result.Value!.Rentals[0].Id}", result.Value) : ToActionResult(result);
    }

    [HttpPost("{rentalId:guid}/return")]
    public async Task<ActionResult<RentalReturnDto>> Return(Guid rentalId, CancellationToken cancellationToken)
    {
        if (!TryGetOperatorId(out var operatorId)) return Forbid();
        return ToActionResult(await sender.Send(new ReturnRentalCommand(rentalId, operatorId), cancellationToken));
    }

    private bool TryGetOperatorId(out Guid operatorId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out operatorId);

    private ActionResult<T> ToActionResult<T>(ServiceOperationResult<T> result) => result.Error switch
    {
        null => Ok(result.Value),
        ServiceOperationError.Validation => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors))),
        ServiceOperationError.NotFound => NotFound(result.Errors),
        ServiceOperationError.Conflict => Conflict(result.Errors),
        _ => Problem()
    };
}
