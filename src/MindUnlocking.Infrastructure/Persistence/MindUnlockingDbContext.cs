using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MindUnlocking.Infrastructure.Identity;
using MindUnlocking.Infrastructure.Outbox;

namespace MindUnlocking.Infrastructure.Persistence;

public sealed class MindUnlockingDbContext(DbContextOptions<MindUnlockingDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MindUnlockingDbContext).Assembly);
    }
}
