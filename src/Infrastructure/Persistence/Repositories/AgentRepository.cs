using GPMH.Application.Common.Interfaces;
using GPMH.Domain.Entities.Agents;
using GPMH.Domain.Entities.Identity;
using GPMH.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GPMH.Infrastructure.Persistence.Repositories;

public sealed class AgentRepository(AppDbContext dbContext) : IAgentRepository
{
    public async Task<bool> ExistsDuplicateAsync(
        string usernameOrMobile,
        string aadhaarBlindIndex,
        string panBlindIndex,
        CancellationToken cancellationToken)
    {
        bool userExists = await dbContext.Users
            .AnyAsync(u => u.Username == usernameOrMobile, cancellationToken)
            .ConfigureAwait(false);

        if (userExists)
        {
            return true;
        }

        return await dbContext.FieldAgents
            .AnyAsync(
                a => a.MobileNo == usernameOrMobile ||
                     a.AadhaarBlindIndex == aadhaarBlindIndex ||
                     a.PanBlindIndex == panBlindIndex,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RegisterAgentAsync(
        User user,
        FieldAgent agent,
        IReadOnlyList<ProtectedAsset> kycAssets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(agent);

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await dbContext.FieldAgents.AddAsync(agent, cancellationToken).ConfigureAwait(false);

        if (kycAssets.Count > 0)
        {
            await dbContext.ProtectedAssets.AddRangeAsync(kycAssets, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<FieldAgent?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.FieldAgents.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

    public Task<FieldAgent?> FindByIdAsync(Guid agentId, CancellationToken cancellationToken) =>
        dbContext.FieldAgents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);

    public Task<User?> FindUserByLoginAsync(string loginIdentifier, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(
            u => u.Username == loginIdentifier || u.Email == loginIdentifier,
            cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<FieldAgent>> ListAllAsync(CancellationToken cancellationToken)
    {
        List<FieldAgent> agents = await dbContext.FieldAgents
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. agents.OrderByDescending(a => a.CreatedAtUtc)];
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

