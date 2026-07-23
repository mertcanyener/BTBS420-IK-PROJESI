using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class IdentityRoleSeederTests
{
    [Fact]
    public async Task SeedAsync_IkiKezCalistirildigindaRolleriTekrarOlusturmaz()
    {
        using var store = new InMemoryRoleStore();
        using var roleManager = new RoleManager<IdentityRole>(
            store,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
        var seeder = new IdentityRoleSeeder(roleManager);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(4, store.CreateCount);
        Assert.Equal(
            SystemRoles.All.Order(StringComparer.Ordinal),
            store.Roles
                .Select(role => role.Name)
                .Order(StringComparer.Ordinal));
    }

    private sealed class InMemoryRoleStore : IRoleStore<IdentityRole>
    {
        private readonly Dictionary<string, IdentityRole> _rolesById = [];

        internal int CreateCount { get; private set; }

        internal IReadOnlyCollection<IdentityRole> Roles => _rolesById.Values;

        public Task<IdentityResult> CreateAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_rolesById.Values.Any(
                    existingRole => string.Equals(
                        existingRole.NormalizedName,
                        role.NormalizedName,
                        StringComparison.Ordinal)))
            {
                return Task.FromResult(
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "DuplicateRoleName",
                            Description = "Rol zaten mevcut."
                        }));
            }

            _rolesById.Add(role.Id, role);
            CreateCount++;

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _rolesById[role.Id] = role;

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _rolesById.Remove(role.Id);

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<string> GetRoleIdAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.Id);
        }

        public Task<string?> GetRoleNameAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.Name);
        }

        public Task SetRoleNameAsync(
            IdentityRole role,
            string? roleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.Name = roleName;

            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedRoleNameAsync(
            IdentityRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.NormalizedName);
        }

        public Task SetNormalizedRoleNameAsync(
            IdentityRole role,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.NormalizedName = normalizedName;

            return Task.CompletedTask;
        }

        public Task<IdentityRole?> FindByIdAsync(
            string roleId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _rolesById.TryGetValue(roleId, out var role);

            return Task.FromResult(role);
        }

        public Task<IdentityRole?> FindByNameAsync(
            string normalizedRoleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = _rolesById.Values.SingleOrDefault(
                existingRole => string.Equals(
                    existingRole.NormalizedName,
                    normalizedRoleName,
                    StringComparison.Ordinal));

            return Task.FromResult(role);
        }

        public void Dispose()
        {
        }
    }
}
