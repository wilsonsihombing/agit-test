using Microsoft.EntityFrameworkCore;
using AccessRequestHub.Api.Models;

namespace AccessRequestHub.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.Name).IsRequired();
            entity.Property(u => u.Role).IsRequired();

            entity.HasOne(u => u.Manager)
                  .WithMany()
                  .HasForeignKey(u => u.ManagerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Application Configuration
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Name).IsUnique();
            entity.Property(a => a.Name).IsRequired();

            entity.HasOne(a => a.SystemOwner)
                  .WithMany()
                  .HasForeignKey(a => a.SystemOwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AccessRequest Configuration
        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            // Idempotency constraint: ClientRequestId must be UNIQUE
            entity.HasIndex(r => r.ClientRequestId).IsUnique();
            entity.Property(r => r.ClientRequestId).IsRequired();
            entity.Property(r => r.Environment).IsRequired();
            entity.Property(r => r.AccessLevel).IsRequired();
            entity.Property(r => r.Justification).IsRequired();
            entity.Property(r => r.Status).IsRequired();
            entity.Property(r => r.PolicyVersion).IsRequired().HasDefaultValue("v1");

            // Optimistic concurrency token
            entity.Property(r => r.Version).IsConcurrencyToken();

            entity.HasOne(r => r.Requester)
                  .WithMany()
                  .HasForeignKey(r => r.RequesterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Application)
                  .WithMany()
                  .HasForeignKey(r => r.ApplicationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CurrentAssignee)
                  .WithMany()
                  .HasForeignKey(r => r.CurrentAssigneeId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditEvent Configuration (Append-only)
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired();
            entity.Property(e => e.ToStatus).IsRequired();

            entity.HasOne(e => e.Request)
                  .WithMany(r => r.AuditEvents)
                  .HasForeignKey(e => e.RequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Actor)
                  .WithMany()
                  .HasForeignKey(e => e.ActorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed Master Data & Demo Users
        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        var fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var bob = new User
        {
            Id = "usr_bob",
            Email = "bob@example.local",
            Name = "Bob (Manager)",
            Role = Roles.Manager,
            ManagerId = null,
            CreatedAt = fixedTime
        };

        var alice = new User
        {
            Id = "usr_alice",
            Email = "alice@example.local",
            Name = "Alice (Requester)",
            Role = Roles.Employee,
            ManagerId = "usr_bob",
            CreatedAt = fixedTime
        };

        var carol = new User
        {
            Id = "usr_carol",
            Email = "carol@example.local",
            Name = "Carol (System Owner CRM)",
            Role = Roles.SystemOwner,
            ManagerId = null,
            CreatedAt = fixedTime
        };

        var dana = new User
        {
            Id = "usr_dana",
            Email = "dana@example.local",
            Name = "Dana (System Owner Finance)",
            Role = Roles.SystemOwner,
            ManagerId = null,
            CreatedAt = fixedTime
        };

        var erin = new User
        {
            Id = "usr_erin",
            Email = "erin@example.local",
            Name = "Erin (Admin/Auditor)",
            Role = Roles.AdminAuditor,
            ManagerId = null,
            CreatedAt = fixedTime
        };

        modelBuilder.Entity<User>().HasData(bob, alice, carol, dana, erin);

        var crm = new Application
        {
            Id = "app_crm",
            Name = "CRM",
            SystemOwnerId = "usr_carol",
            CreatedAt = fixedTime
        };

        var finance = new Application
        {
            Id = "app_finance",
            Name = "Finance Portal",
            SystemOwnerId = "usr_dana",
            CreatedAt = fixedTime
        };

        modelBuilder.Entity<Application>().HasData(crm, finance);
    }
}
