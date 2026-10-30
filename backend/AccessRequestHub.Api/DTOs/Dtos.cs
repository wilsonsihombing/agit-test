using System.ComponentModel.DataAnnotations;

namespace AccessRequestHub.Api.DTOs;

public class CreateRequestDto
{
    [Required(ErrorMessage = "ClientRequestId is required for idempotency")]
    public string ClientRequestId { get; set; } = string.Empty;

    [Required(ErrorMessage = "ApplicationId is required")]
    public string ApplicationId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Environment is required")]
    [RegularExpression("^(NonProduction|Production)$", ErrorMessage = "Environment must be NonProduction or Production")]
    public string Environment { get; set; } = string.Empty;

    [Required(ErrorMessage = "AccessLevel is required")]
    [RegularExpression("^(Read|Admin)$", ErrorMessage = "AccessLevel must be Read or Admin")]
    public string AccessLevel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Justification is required")]
    public string Justification { get; set; } = string.Empty;
}

public class ApproveRequestDto
{
    [Required(ErrorMessage = "ExpectedVersion is required for optimistic concurrency control")]
    public int ExpectedVersion { get; set; }
}

public class RejectRequestDto
{
    [Required(ErrorMessage = "ExpectedVersion is required for optimistic concurrency control")]
    public int ExpectedVersion { get; set; }

    [Required(ErrorMessage = "Reject reason is required")]
    [MinLength(3, ErrorMessage = "Reject reason must be at least 3 characters")]
    public string Reason { get; set; } = string.Empty;
}

public class RequestResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientRequestId { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public bool IsHighRisk { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string? CurrentAssigneeId { get; set; }
    public string? CurrentAssigneeName { get; set; }
    public int Version { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AuditEventDto
{
    public int Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int RequestVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RequestDetailResponseDto : RequestResponseDto
{
    public List<AuditEventDto> AuditTrail { get; set; } = new();
}

public class ErrorDetail
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ErrorResponseDto
{
    public ErrorDetail Error { get; set; } = new();
}
