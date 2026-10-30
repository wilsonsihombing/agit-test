using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AccessRequestHub.Api.Data;
using AccessRequestHub.Api.DTOs;
using AccessRequestHub.Api.Models;
using Xunit;

namespace AccessRequestHub.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public TestWebApplicationFactory()
    {
        // In-memory SQLite connection kept open for the test database lifetime
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register SQLite in-memory with shared connection
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Ensure database and seeds are built
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Close();
        _connection.Dispose();
    }
}

public class AccessRequestWorkflowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AccessRequestWorkflowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string userId)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("x-user-id", userId);
        return request;
    }

    [Fact]
    public async Task Scenario1_StandardRequest_ApprovedDirectlyByManager()
    {
        // 1. Alice creates standard request (CRM, NonProduction, Read)
        var createPayload = new CreateRequestDto
        {
            ClientRequestId = $"req-std-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Standard read access for day-to-day operations"
        };

        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(createPayload);
        var submitRes = await _client.SendAsync(submitReq);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;
        var initialStatus = submitJson.GetProperty("data").GetProperty("status").GetString();
        var initialAssignee = submitJson.GetProperty("data").GetProperty("currentAssigneeId").GetString();
        var version = submitJson.GetProperty("data").GetProperty("version").GetInt32();

        Assert.Equal(RequestStatuses.PendingManager, initialStatus);
        Assert.Equal("usr_bob", initialAssignee);
        Assert.Equal(1, version);

        // 2. Bob (Manager) approves request
        var approvePayload = new ApproveRequestDto { ExpectedVersion = version };
        var approveReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_bob");
        approveReq.Content = JsonContent.Create(approvePayload);
        var approveRes = await _client.SendAsync(approveReq);

        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approveJson = await approveRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var finalStatus = approveJson.GetProperty("data").GetProperty("status").GetString();

        // Non-high-risk becomes APPROVED immediately
        Assert.Equal(RequestStatuses.Approved, finalStatus);
        Assert.True(approveJson.GetProperty("data").GetProperty("currentAssigneeId").ValueKind == JsonValueKind.Null);

        // 3. Verify detail and audit trail
        var detailReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/requests/{requestId}", "usr_alice");
        var detailRes = await _client.SendAsync(detailReq);
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);

        var detailJson = await detailRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var auditTrail = detailJson.GetProperty("data").GetProperty("auditTrail");
        Assert.Equal(2, auditTrail.GetArrayLength()); // SUBMIT + MANAGER_APPROVE
    }

    [Fact]
    public async Task Scenario2_ProductionRequest_HighRisk_RequiresSystemOwnerApproval()
    {
        // 1. Alice creates Production request (High-Risk)
        var createPayload = new CreateRequestDto
        {
            ClientRequestId = $"req-prod-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "Production",
            AccessLevel = "Read",
            Justification = "Needs production access for hotfix"
        };

        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(createPayload);
        var submitRes = await _client.SendAsync(submitReq);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;
        var isHighRisk = submitJson.GetProperty("data").GetProperty("isHighRisk").GetBoolean();
        var version = submitJson.GetProperty("data").GetProperty("version").GetInt32();

        Assert.True(isHighRisk);

        // 2. Bob (Manager) approves -> should transition to PENDING_SYSTEM_OWNER (Carol)
        var approveBob = new ApproveRequestDto { ExpectedVersion = version };
        var bobReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_bob");
        bobReq.Content = JsonContent.Create(approveBob);
        var bobRes = await _client.SendAsync(bobReq);

        Assert.Equal(HttpStatusCode.OK, bobRes.StatusCode);
        var bobJson = await bobRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var intermediateStatus = bobJson.GetProperty("data").GetProperty("status").GetString();
        var intermediateAssignee = bobJson.GetProperty("data").GetProperty("currentAssigneeId").GetString();
        var nextVersion = bobJson.GetProperty("data").GetProperty("version").GetInt32();

        Assert.Equal(RequestStatuses.PendingSystemOwner, intermediateStatus);
        Assert.Equal("usr_carol", intermediateAssignee);
        Assert.Equal(2, nextVersion);

        // 3. Carol (CRM System Owner) approves -> should transition to APPROVED
        var approveCarol = new ApproveRequestDto { ExpectedVersion = nextVersion };
        var carolReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_carol");
        carolReq.Content = JsonContent.Create(approveCarol);
        var carolRes = await _client.SendAsync(carolReq);

        Assert.Equal(HttpStatusCode.OK, carolRes.StatusCode);
        var carolJson = await carolRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(RequestStatuses.Approved, carolJson.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(3, carolJson.GetProperty("data").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Scenario3_AdminAccess_HighRisk_FinancePortal_RequiresDanaApproval()
    {
        // 1. Alice requests Admin access on Finance Portal (High-Risk)
        var createPayload = new CreateRequestDto
        {
            ClientRequestId = $"req-admin-{Guid.NewGuid()}",
            ApplicationId = "app_finance",
            Environment = "NonProduction",
            AccessLevel = "Admin",
            Justification = "Needs finance portal admin access"
        };

        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(createPayload);
        var submitRes = await _client.SendAsync(submitReq);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;

        // 2. Bob approves -> moves to Dana (Finance System Owner)
        var bobReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_bob");
        bobReq.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 1 });
        var bobRes = await _client.SendAsync(bobReq);

        Assert.Equal(HttpStatusCode.OK, bobRes.StatusCode);
        var bobJson = await bobRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(RequestStatuses.PendingSystemOwner, bobJson.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("usr_dana", bobJson.GetProperty("data").GetProperty("currentAssigneeId").GetString());

        // 3. Dana approves -> Approved
        var danaReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_dana");
        danaReq.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 2 });
        var danaRes = await _client.SendAsync(danaReq);

        Assert.Equal(HttpStatusCode.OK, danaRes.StatusCode);
        var danaJson = await danaRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(RequestStatuses.Approved, danaJson.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Scenario4_UnauthorizedApproval_RejectedWithForbidden403()
    {
        // Alice creates request
        var createPayload = new CreateRequestDto
        {
            ClientRequestId = $"req-unauth-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Unauthorized test"
        };

        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(createPayload);
        var submitRes = await _client.SendAsync(submitReq);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;

        // Dana (unauthorized user, neither manager nor system owner of CRM) attempts approval
        var danaReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_dana");
        danaReq.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 1 });
        var danaRes = await _client.SendAsync(danaReq);

        Assert.Equal(HttpStatusCode.Forbidden, danaRes.StatusCode);
        var errJson = await danaRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("FORBIDDEN", errJson.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scenario5_SelfApproval_PreventedWithForbidden403()
    {
        // Alice creates request
        var createPayload = new CreateRequestDto
        {
            ClientRequestId = $"req-self-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Self-approval test"
        };

        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(createPayload);
        var submitRes = await _client.SendAsync(submitReq);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;

        // Alice attempts to approve her own request
        var aliceApproveReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_alice");
        aliceApproveReq.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 1 });
        var aliceRes = await _client.SendAsync(aliceApproveReq);

        Assert.Equal(HttpStatusCode.Forbidden, aliceRes.StatusCode);
        var errJson = await aliceRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("FORBIDDEN", errJson.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scenario6_DuplicateSubmit_Idempotent_ReturnsExistingRecord()
    {
        var clientRequestId = $"req-idempotent-{Guid.NewGuid()}";
        var payload = new CreateRequestDto
        {
            ClientRequestId = clientRequestId,
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Idempotency test payload"
        };

        // First submit -> 201 Created
        var req1 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        req1.Content = JsonContent.Create(payload);
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var json1 = await res1.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id1 = json1.GetProperty("data").GetProperty("id").GetString();
        Assert.False(json1.GetProperty("isDuplicate").GetBoolean());

        // Duplicate submit -> 200 OK with same ID
        var req2 = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        req2.Content = JsonContent.Create(payload);
        var res2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var json2 = await res2.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id2 = json2.GetProperty("data").GetProperty("id").GetString();
        Assert.True(json2.GetProperty("isDuplicate").GetBoolean());

        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task Scenario7_ConcurrentAction_OptimisticConcurrency_SecondFailsWithConflict409()
    {
        // 1. Create request
        var payload = new CreateRequestDto
        {
            ClientRequestId = $"req-conflict-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Conflict test"
        };
        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(payload);
        var submitRes = await _client.SendAsync(submitReq);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;

        // 2. Action 1: Bob approves with ExpectedVersion = 1 -> succeeds
        var action1 = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_bob");
        action1.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 1 });
        var res1 = await _client.SendAsync(action1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 3. Action 2: Stale concurrent action with ExpectedVersion = 1 -> 409 Conflict
        var action2 = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/reject", "usr_bob");
        action2.Content = JsonContent.Create(new RejectRequestDto { ExpectedVersion = 1, Reason = "Late rejection" });
        var res2 = await _client.SendAsync(action2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
        var errJson = await res2.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("CONFLICT", errJson.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scenario8_RejectedRequest_StoresReasonAndTerminates()
    {
        // 1. Create request
        var payload = new CreateRequestDto
        {
            ClientRequestId = $"req-reject-{Guid.NewGuid()}",
            ApplicationId = "app_crm",
            Environment = "NonProduction",
            AccessLevel = "Read",
            Justification = "Rejection test"
        };
        var submitReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/requests", "usr_alice");
        submitReq.Content = JsonContent.Create(payload);
        var submitRes = await _client.SendAsync(submitReq);
        var submitJson = await submitRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var requestId = submitJson.GetProperty("data").GetProperty("id").GetString()!;

        // 2. Reject without reason -> 400 Bad Request
        var rejectNoReason = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/reject", "usr_bob");
        rejectNoReason.Content = JsonContent.Create(new RejectRequestDto { ExpectedVersion = 1, Reason = "" });
        var resNoReason = await _client.SendAsync(rejectNoReason);
        Assert.Equal(HttpStatusCode.BadRequest, resNoReason.StatusCode);

        // 3. Bob rejects with reason -> succeeds
        var rejectReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/reject", "usr_bob");
        rejectReq.Content = JsonContent.Create(new RejectRequestDto { ExpectedVersion = 1, Reason = "Insufficient justification provided" });
        var rejectRes = await _client.SendAsync(rejectReq);
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);

        var rejectJson = await rejectRes.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(RequestStatuses.Rejected, rejectJson.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal("Insufficient justification provided", rejectJson.GetProperty("data").GetProperty("rejectReason").GetString());

        // 4. Any subsequent transition on terminal state is rejected
        var retryApprove = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/requests/{requestId}/approve", "usr_bob");
        retryApprove.Content = JsonContent.Create(new ApproveRequestDto { ExpectedVersion = 2 });
        var retryRes = await _client.SendAsync(retryApprove);
        Assert.Equal(HttpStatusCode.BadRequest, retryRes.StatusCode);
    }
}
