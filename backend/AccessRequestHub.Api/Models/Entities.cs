using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccessRequestHub.Api.Models;

public static class Roles
{
    public const string Employee = "EMPLOYEE";
    public const string Manager = "MANAGER";
    public const string SystemOwner = "SYSTEM_OWNER";
    public const string AdminAuditor = "ADMIN_AUDITOR";
}

public static class RequestStatuses
{
    public const string PendingManager = "PENDING_MANAGER";
    public const string PendingSystemOwner = "PENDING_SYSTEM_OWNER";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

public static class AuditActions
{
    public const string Submit = "SUBMIT";
    public const string ManagerApprove = "MANAGER_APPROVE";
    public const string ManagerReject = "MANAGER_REJECT";
    public const string SystemOwnerApprove = "SYSTEM_OWNER_APPROVE";
    public const string SystemOwnerReject = "SYSTEM_OWNER_REJECT";
}

public class User
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public string? ManagerId { get; set; }
    public User? Manager { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Application
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SystemOwnerId { get; set; } = string.Empty;
    public User? SystemOwner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AccessRequest
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ClientRequestId { get; set; } = string.Empty;

    [Required]
    public string RequesterId { get; set; } = string.Empty;
    public User? Requester { get; set; }

    [Required]
    public string ApplicationId { get; set; } = string.Empty;
    public Application? Application { get; set; }

    [Required]
    public string Environment { get; set; } = string.Empty; // NonProduction or Production

    [Required]
    public string AccessLevel { get; set; } = string.Empty; // Read or Admin

    [Required]
    public string Justification { get; set; } = string.Empty;

    public bool IsHighRisk { get; set; }

    [Required]
    public string Status { get; set; } = RequestStatuses.PendingManager;

    [Required]
    public string PolicyVersion { get; set; } = "v1";

    public string? CurrentAssigneeId { get; set; }
    public User? CurrentAssignee { get; set; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Incrementing this ensures stale actions fail with DbUpdateConcurrencyException / 409 Conflict.
    /// </summary>
    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    public string? RejectReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AuditEvent> AuditEvents { get; set; } = new();
}

public class AuditEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string RequestId { get; set; } = string.Empty;
    public AccessRequest? Request { get; set; }

    [Required]
    public string ActorId { get; set; } = string.Empty;
    public User? Actor { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    [Required]
    public string ToStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public int RequestVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
