using Microsoft.EntityFrameworkCore;
using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.DTOs;
using AccessRequestHub.Api.Exceptions;
using AccessRequestHub.Api.Models;

namespace AccessRequestHub.Api.Services;

public class AccessRequestService
{
    private readonly AppDbContext _context;

    public AccessRequestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        return await _context.Users.Include(u => u.Manager).FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.Include(u => u.Manager).FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderBy(u => u.Name).ToListAsync();
    }

    public async Task<List<Application>> GetAllApplicationsAsync()
    {
        return await _context.Applications.Include(a => a.SystemOwner).OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<RequestDetailResponseDto?> GetRequestByIdAsync(string id)
    {
        var request = await _context.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.CurrentAssignee)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null) return null;

        var auditEvents = await _context.AuditEvents
            .Include(a => a.Actor)
            .Where(a => a.RequestId == id)
            .OrderBy(a => a.Id)
            .Select(a => new AuditEventDto
            {
                Id = a.Id,
                RequestId = a.RequestId,
                ActorId = a.ActorId,
                ActorName = a.Actor != null ? a.Actor.Name : a.ActorId,
                Action = a.Action,
                FromStatus = a.FromStatus,
                ToStatus = a.ToStatus,
                Reason = a.Reason,
                RequestVersion = a.RequestVersion,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new RequestDetailResponseDto
        {
            Id = request.Id,
            ClientRequestId = request.ClientRequestId,
            RequesterId = request.RequesterId,
            RequesterName = request.Requester?.Name ?? request.RequesterId,
            ApplicationId = request.ApplicationId,
            ApplicationName = request.Application?.Name ?? request.ApplicationId,
            Environment = request.Environment,
            AccessLevel = request.AccessLevel,
            Justification = request.Justification,
            IsHighRisk = request.IsHighRisk,
            Status = request.Status,
            PolicyVersion = request.PolicyVersion,
            CurrentAssigneeId = request.CurrentAssigneeId,
            CurrentAssigneeName = request.CurrentAssignee?.Name,
            Version = request.Version,
            RejectReason = request.RejectReason,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt,
            AuditTrail = auditEvents
        };
    }

    public async Task<List<RequestResponseDto>> ListRequestsAsync(string? requesterId, string? assigneeId, bool viewAll)
    {
        var query = _context.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.CurrentAssignee)
            .AsQueryable();

        if (!viewAll)
        {
            if (!string.IsNullOrEmpty(requesterId))
            {
                query = query.Where(r => r.RequesterId == requesterId);
            }
            else if (!string.IsNullOrEmpty(assigneeId))
            {
                query = query.Where(r => r.CurrentAssigneeId == assigneeId);
            }
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RequestResponseDto
            {
                Id = r.Id,
                ClientRequestId = r.ClientRequestId,
                RequesterId = r.RequesterId,
                RequesterName = r.Requester != null ? r.Requester.Name : r.RequesterId,
                ApplicationId = r.ApplicationId,
                ApplicationName = r.Application != null ? r.Application.Name : r.ApplicationId,
                Environment = r.Environment,
                AccessLevel = r.AccessLevel,
                Justification = r.Justification,
                IsHighRisk = r.IsHighRisk,
                Status = r.Status,
                PolicyVersion = r.PolicyVersion,
                CurrentAssigneeId = r.CurrentAssigneeId,
                CurrentAssigneeName = r.CurrentAssignee != null ? r.CurrentAssignee.Name : null,
                Version = r.Version,
                RejectReason = r.RejectReason,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }

    public static bool IsHighRisk(string environment, string accessLevel)
    {
        return string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(accessLevel, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(RequestResponseDto Request, bool IsDuplicate)> CreateRequestAsync(User requester, CreateRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientRequestId))
        {
            throw new DomainValidationException("ClientRequestId is required for idempotency");
        }
        if (string.IsNullOrWhiteSpace(dto.Justification))
        {
            throw new DomainValidationException("Justification is required");
        }

        // 1. Idempotency Check: if ClientRequestId already exists, return existing request
        var existing = await _context.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.CurrentAssignee)
            .FirstOrDefaultAsync(r => r.ClientRequestId == dto.ClientRequestId.Trim());

        if (existing != null)
        {
            return (MapToDto(existing), true);
        }

        // 2. Validate Application
        var app = await _context.Applications.FirstOrDefaultAsync(a => a.Id == dto.ApplicationId);
        if (app == null)
        {
            throw new DomainNotFoundException($"Application '{dto.ApplicationId}' not found");
        }

        // 3. Validate Requester's manager
        if (string.IsNullOrEmpty(requester.ManagerId))
        {
            throw new DomainValidationException($"Requester '{requester.Name}' has no manager assigned to review this request");
        }

        bool isHighRisk = IsHighRisk(dto.Environment, dto.AccessLevel);
        string requestId = $"req_{Guid.NewGuid():N}";
        int initialVersion = 1;

        var newRequest = new AccessRequest
        {
            Id = requestId,
            ClientRequestId = dto.ClientRequestId.Trim(),
            RequesterId = requester.Id,
            ApplicationId = app.Id,
            Environment = dto.Environment,
            AccessLevel = dto.AccessLevel,
            Justification = dto.Justification.Trim(),
            IsHighRisk = isHighRisk,
            Status = RequestStatuses.PendingManager,
            PolicyVersion = "v1",
            CurrentAssigneeId = requester.ManagerId,
            Version = initialVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var initialAudit = new AuditEvent
        {
            RequestId = requestId,
            ActorId = requester.Id,
            Action = AuditActions.Submit,
            FromStatus = null,
            ToStatus = RequestStatuses.PendingManager,
            Reason = null,
            RequestVersion = initialVersion,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.AccessRequests.Add(newRequest);
            _context.AuditEvents.Add(initialAudit);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // In case of a race condition on concurrent duplicate submit, fetch existing record
            var raceExisting = await _context.AccessRequests
                .Include(r => r.Requester)
                .Include(r => r.Application)
                .Include(r => r.CurrentAssignee)
                .FirstOrDefaultAsync(r => r.ClientRequestId == dto.ClientRequestId.Trim());

            if (raceExisting != null)
            {
                return (MapToDto(raceExisting), true);
            }
            throw;
        }

        // Populate navigations for response
        newRequest.Requester = requester;
        newRequest.Application = app;
        newRequest.CurrentAssignee = await _context.Users.FirstOrDefaultAsync(u => u.Id == requester.ManagerId);

        return (MapToDto(newRequest), false);
    }

    public async Task<RequestResponseDto> ApproveRequestAsync(User actor, string requestId, int expectedVersion)
    {
        var request = await _context.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.CurrentAssignee)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            throw new DomainNotFoundException($"Access request '{requestId}' not found");
        }

        // 1. Optimistic concurrency check
        if (request.Version != expectedVersion)
        {
            throw new DomainConflictException($"Version conflict! Current version is {request.Version}, but expected {expectedVersion}. Refresh the page to see latest state.");
        }

        // 2. Terminal check
        if (request.Status is RequestStatuses.Approved or RequestStatuses.Rejected)
        {
            throw new DomainValidationException($"Cannot transition request in terminal state: {request.Status}");
        }

        // 3. Self-approval prevention
        if (actor.Id == request.RequesterId)
        {
            throw new DomainForbiddenException("Requester is not permitted to approve their own access request");
        }

        string fromStatus = request.Status;
        string newStatus;
        string? nextAssigneeId;
        string auditAction;

        if (request.Status == RequestStatuses.PendingManager)
        {
            if (actor.Id != request.CurrentAssigneeId)
            {
                throw new DomainForbiddenException("Only the assigned manager can process this approval stage");
            }

            auditAction = AuditActions.ManagerApprove;

            if (request.IsHighRisk)
            {
                newStatus = RequestStatuses.PendingSystemOwner;
                nextAssigneeId = request.Application!.SystemOwnerId;
            }
            else
            {
                newStatus = RequestStatuses.Approved;
                nextAssigneeId = null;
            }
        }
        else if (request.Status == RequestStatuses.PendingSystemOwner)
        {
            if (actor.Id != request.CurrentAssigneeId || actor.Id != request.Application!.SystemOwnerId)
            {
                throw new DomainForbiddenException("Only the designated System Owner for this application can approve this stage");
            }

            auditAction = AuditActions.SystemOwnerApprove;
            newStatus = RequestStatuses.Approved;
            nextAssigneeId = null;
        }
        else
        {
            throw new DomainValidationException($"Illegal transition from status {request.Status}");
        }

        int nextVersion = request.Version + 1;
        request.Status = newStatus;
        request.CurrentAssigneeId = nextAssigneeId;
        request.Version = nextVersion;
        request.UpdatedAt = DateTime.UtcNow;

        var auditEvent = new AuditEvent
        {
            RequestId = request.Id,
            ActorId = actor.Id,
            Action = auditAction,
            FromStatus = fromStatus,
            ToStatus = newStatus,
            Reason = null,
            RequestVersion = nextVersion,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditEvents.Add(auditEvent);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainConflictException("A concurrent update occurred on this request. Please refresh and try again.");
        }

        // Reload assignee navigation
        request.CurrentAssignee = nextAssigneeId != null ? await _context.Users.FirstOrDefaultAsync(u => u.Id == nextAssigneeId) : null;

        return MapToDto(request);
    }

    public async Task<RequestResponseDto> RejectRequestAsync(User actor, string requestId, int expectedVersion, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Reject reason is required");
        }

        var request = await _context.AccessRequests
            .Include(r => r.Requester)
            .Include(r => r.Application)
            .Include(r => r.CurrentAssignee)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            throw new DomainNotFoundException($"Access request '{requestId}' not found");
        }

        // 1. Optimistic concurrency check
        if (request.Version != expectedVersion)
        {
            throw new DomainConflictException($"Version conflict! Current version is {request.Version}, but expected {expectedVersion}. Refresh the page to see latest state.");
        }

        // 2. Terminal check
        if (request.Status is RequestStatuses.Approved or RequestStatuses.Rejected)
        {
            throw new DomainValidationException($"Cannot transition request in terminal state: {request.Status}");
        }

        // 3. Self-action check
        if (actor.Id == request.RequesterId)
        {
            throw new DomainForbiddenException("Requester cannot process approval or rejection on their own request");
        }

        // 4. Assigned approver check
        if (actor.Id != request.CurrentAssigneeId)
        {
            throw new DomainForbiddenException("Only the currently assigned approver can reject this request");
        }

        string fromStatus = request.Status;
        string auditAction = request.Status == RequestStatuses.PendingManager
            ? AuditActions.ManagerReject
            : AuditActions.SystemOwnerReject;

        int nextVersion = request.Version + 1;
        request.Status = RequestStatuses.Rejected;
        request.RejectReason = reason.Trim();
        request.CurrentAssigneeId = null;
        request.Version = nextVersion;
        request.UpdatedAt = DateTime.UtcNow;

        var auditEvent = new AuditEvent
        {
            RequestId = request.Id,
            ActorId = actor.Id,
            Action = auditAction,
            FromStatus = fromStatus,
            ToStatus = RequestStatuses.Rejected,
            Reason = reason.Trim(),
            RequestVersion = nextVersion,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditEvents.Add(auditEvent);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainConflictException("A concurrent update occurred on this request. Please refresh and try again.");
        }

        request.CurrentAssignee = null;
        return MapToDto(request);
    }

    private static RequestResponseDto MapToDto(AccessRequest r)
    {
        return new RequestResponseDto
        {
            Id = r.Id,
            ClientRequestId = r.ClientRequestId,
            RequesterId = r.RequesterId,
            RequesterName = r.Requester != null ? r.Requester.Name : r.RequesterId,
            ApplicationId = r.ApplicationId,
            ApplicationName = r.Application != null ? r.Application.Name : r.ApplicationId,
            Environment = r.Environment,
            AccessLevel = r.AccessLevel,
            Justification = r.Justification,
            IsHighRisk = r.IsHighRisk,
            Status = r.Status,
            PolicyVersion = r.PolicyVersion,
            CurrentAssigneeId = r.CurrentAssigneeId,
            CurrentAssigneeName = r.CurrentAssignee != null ? r.CurrentAssignee.Name : null,
            Version = r.Version,
            RejectReason = r.RejectReason,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }
}
