using EngineeringPlayground.Cqrs.Api.Queries;
using EngineeringPlayground.Cqrs.Api.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringPlayground.Cqrs.Api.Controllers;

[ApiController]
[Route("inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly GetInventoryDashboardHandler _getInventoryDashboardHandler;

    public InventoryController(GetInventoryDashboardHandler getInventoryDashboardHandler)
    {
        _getInventoryDashboardHandler = getInventoryDashboardHandler;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<InventoryDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        return await _getInventoryDashboardHandler.HandleAsync(cancellationToken);
    }
}
