using Microsoft.AspNetCore.Mvc;
using AccessRequestHub.Api.DTOs;
using AccessRequestHub.Api.Exceptions;
using AccessRequestHub.Api.Middleware;
using AccessRequestHub.Api.Models;
using AccessRequestHub.Api.Services;

namespace AccessRequestHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AccessRequestService _service;

    public UsersController(AccessRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _service.GetAllUsersAsync();
        return Ok(new { data = users });
    }
}

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly AccessRequestService _service;

    public ApplicationsController(AccessRequestService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetApplications()
    {
        var apps = await _service.GetAllApplicationsAsync();
        return Ok(new { data = apps });
    }
}

[ApiController]
[Route("api")]
public class RequestsController : ControllerBase
{
    private readonly AccessRequestService _service;

    public RequestsController(AccessRequestService service)
    {
        _service = service;
    }

    private User RequireUser()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            throw new DomainUnauthorizedException("Missing or invalid 'x-user-id' / 'x-user-email' authentication header");
        }
        return user;
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateRequestDto dto)
    {
        var user = RequireUser();
        var (request, isDuplicate) = await _service.CreateRequestAsync(user, dto);

        return StatusCode(isDuplicate ? StatusCodes.Status200OK : StatusCodes.Status201Created, new
        {
            data = request,
            isDuplicate
        });
    }

    [HttpGet("requests")]
    public async Task<IActionResult> ListRequests([FromQuery] string? scope)
    {
        var user = RequireUser();

        bool viewAll = user.Role == Roles.AdminAuditor || string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        string? requesterFilter = viewAll ? null : user.Id;

        var requests = await _service.ListRequestsAsync(requesterFilter, null, viewAll);
        return Ok(new { data = requests });
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox()
    {
        var user = RequireUser();
        var pending = await _service.ListRequestsAsync(null, user.Id, false);
        return Ok(new { data = pending });
    }

    [HttpGet("requests/{id}")]
    public async Task<IActionResult> GetRequestDetail(string id)
    {
        RequireUser();
        var detail = await _service.GetRequestByIdAsync(id);
        if (detail == null)
        {
            throw new DomainNotFoundException($"Access request '{id}' not found");
        }
        return Ok(new { data = detail });
    }

    [HttpPost("requests/{id}/approve")]
    public async Task<IActionResult> ApproveRequest(string id, [FromBody] ApproveRequestDto dto)
    {
        var user = RequireUser();
        var updated = await _service.ApproveRequestAsync(user, id, dto.ExpectedVersion);
        return Ok(new { data = updated });
    }

    [HttpPost("requests/{id}/reject")]
    public async Task<IActionResult> RejectRequest(string id, [FromBody] RejectRequestDto dto)
    {
        var user = RequireUser();
        var updated = await _service.RejectRequestAsync(user, id, dto.ExpectedVersion, dto.Reason);
        return Ok(new { data = updated });
    }
}
