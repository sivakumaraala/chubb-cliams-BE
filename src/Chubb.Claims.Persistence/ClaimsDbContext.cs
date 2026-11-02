using Chubb.Claims.Application;
using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Persistence;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimHistoryEntry> History => Set<ClaimHistoryEntry>();
    public DbSet<ClaimDecision> Decisions => Set<ClaimDecision>();
    public DbSet<ClaimInformationRequest> InformationRequests => Set<ClaimInformationRequest>();
    public DbSet<ClaimSettlementPayment> SettlementPayments => Set<ClaimSettlementPayment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ClaimNotification> Notifications => Set<ClaimNotification>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var claim = modelBuilder.Entity<Claim>();
        claim.ToTable("Claims");
        claim.HasKey(x => x.Id);
        claim.Property(x => x.Id).ValueGeneratedNever();
        claim.Property(x => x.Reference).HasMaxLength(40).IsRequired();
        claim.HasIndex(x => x.Reference).IsUnique();
        claim.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        claim.Property(x => x.AssignedOfficerId).HasMaxLength(64);
        claim.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        claim.Property(x => x.Market).HasMaxLength(64).IsRequired();
        claim.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        claim.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        claim.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        claim.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
        claim.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        claim.Property(x => x.ReserveAmount).HasPrecision(18, 2);
        claim.Property(x => x.RowVersion).IsRowVersion();
        claim.HasIndex(x => x.OwnerId);
        claim.HasIndex(x => new { x.Status, x.AssignedOfficerId });
        claim.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        claim.Navigation(x => x.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        claim.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        claim.Navigation(x => x.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);
        claim.HasMany(x => x.InformationRequests).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        claim.Navigation(x => x.InformationRequests).HasField("_informationRequests").UsePropertyAccessMode(PropertyAccessMode.Field);
        claim.HasMany(x => x.SettlementPayments).WithOne().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
        claim.Navigation(x => x.SettlementPayments).HasField("_settlementPayments").UsePropertyAccessMode(PropertyAccessMode.Field);

        var payment = modelBuilder.Entity<ClaimSettlementPayment>();
        payment.ToTable("ClaimSettlementPayments");
        payment.HasKey(x => x.Id);
        payment.Property(x => x.Id).ValueGeneratedNever();
        payment.Property(x => x.OfficerId).HasMaxLength(64).IsRequired();
        payment.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        payment.Property(x => x.Amount).HasPrecision(18, 2);
        payment.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
        payment.HasIndex(x => new { x.ClaimId, x.ExternalReference }).IsUnique();

        var information = modelBuilder.Entity<ClaimInformationRequest>();
        information.ToTable("ClaimInformationRequests");
        information.HasKey(x => x.Id);
        information.Property(x => x.Id).ValueGeneratedNever();
        information.Property(x => x.OfficerId).HasMaxLength(64).IsRequired();
        information.Property(x => x.Question).HasMaxLength(2000).IsRequired();
        information.Property(x => x.Response).HasMaxLength(2000);
        information.HasIndex(x => new { x.ClaimId, x.RequestedAtUtc });

        var history = modelBuilder.Entity<ClaimHistoryEntry>();
        history.ToTable("ClaimHistory");
        history.HasKey(x => x.Id);
        history.Property(x => x.Id).ValueGeneratedNever();
        history.Property(x => x.ActorId).HasMaxLength(64).IsRequired();
        history.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
        history.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        history.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30);
        history.Property(x => x.ReserveBefore).HasPrecision(18, 2);
        history.Property(x => x.ReserveAfter).HasPrecision(18, 2);
        history.HasIndex(x => new { x.ClaimId, x.OccurredAtUtc });

        var decision = modelBuilder.Entity<ClaimDecision>();
        decision.ToTable("ClaimDecisions");
        decision.HasKey(x => x.Id);
        decision.Property(x => x.Id).ValueGeneratedNever();
        decision.Property(x => x.OfficerId).HasMaxLength(64).IsRequired();
        decision.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30);
        decision.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        decision.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        decision.HasIndex(x => x.ClaimId).IsUnique();

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("OutboxMessages");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Id).ValueGeneratedNever();
        outbox.Property(x => x.Payload).IsRequired();
        outbox.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
        outbox.HasIndex(x => new { x.PublishedAtUtc, x.NextAttemptAtUtc });

        var notification = modelBuilder.Entity<ClaimNotification>();
        notification.ToTable("ClaimNotifications");
        notification.HasKey(x => x.Id);
        notification.Property(x => x.Id).ValueGeneratedNever();
        notification.Property(x => x.OwnerId).HasMaxLength(64).IsRequired();
        notification.Property(x => x.Message).HasMaxLength(500).IsRequired();
        notification.HasIndex(x => x.EventId).IsUnique();
        notification.HasIndex(x => new { x.ClaimId, x.CreatedAtUtc });
        notification.HasOne<Claim>().WithMany().HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);

        var processed = modelBuilder.Entity<ProcessedEvent>();
        processed.ToTable("ProcessedEvents");
        processed.HasKey(x => x.EventId);
        processed.Property(x => x.EventId).ValueGeneratedNever();
    }
}
