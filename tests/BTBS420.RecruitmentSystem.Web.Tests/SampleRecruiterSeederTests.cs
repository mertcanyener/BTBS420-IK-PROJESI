using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Identity;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class SampleRecruiterSeederTests
{
    [Fact]
    public async Task SeedAsync_BootstrapKapaliysaKullaniciOlusturmaz()
    {
        using var store = new InMemoryUserStore();
        var (seeder, _) = CreateSeeder(store, enabled: false);

        await seeder.SeedAsync();

        Assert.Empty(store.Users);
    }

    [Fact]
    public async Task SeedAsync_OrnekKullaniciyiIseAlimUzmaniRoluyleOlusturur()
    {
        using var store = new InMemoryUserStore();
        var (seeder, userManager) = CreateSeeder(store, enabled: true);

        await seeder.SeedAsync();

        var createdUser = Assert.Single(store.Users);
        Assert.Equal("uzman@local.test", createdUser.Email);
        Assert.True(createdUser.IsActive);
        Assert.True(
            await userManager.IsInRoleAsync(
                createdUser,
                SystemRoles.RecruitmentSpecialist));
    }

    [Fact]
    public async Task SeedAsync_IkiKezCalistirildigindaDuplicateKullaniciOlusturmaz()
    {
        using var store = new InMemoryUserStore();
        var (seeder, _) = CreateSeeder(store, enabled: true);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Single(store.Users);
        Assert.Equal(1, store.CreateCount);
    }

    private static (SampleRecruiterSeeder Seeder, UserManager<ApplicationUser> UserManager) CreateSeeder(
        InMemoryUserStore store,
        bool enabled)
    {
        var userManager = new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var bootstrapOptions = Options.Create(
            new IdentityBootstrapOptions { Enabled = enabled });

        return (new SampleRecruiterSeeder(userManager, bootstrapOptions), userManager);
    }

    private sealed class InMemoryUserStore :
        IUserStore<ApplicationUser>,
        IUserEmailStore<ApplicationUser>,
        IUserPasswordStore<ApplicationUser>,
        IUserRoleStore<ApplicationUser>
    {
        private readonly Dictionary<string, ApplicationUser> _usersById = [];
        private readonly Dictionary<string, HashSet<string>> _rolesByUserId = [];

        internal int CreateCount { get; private set; }

        internal IReadOnlyCollection<ApplicationUser> Users => _usersById.Values;

        internal IReadOnlyCollection<string> GetRoles(string userId)
        {
            return _rolesByUserId.TryGetValue(userId, out var roles)
                ? roles
                : [];
        }

        public Task<IdentityResult> CreateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(user.Id) && _usersById.ContainsKey(user.Id))
            {
                return Task.FromResult(
                    IdentityResult.Failed(
                        new IdentityError { Code = "DuplicateUserId" }));
            }

            if (string.IsNullOrEmpty(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
            }

            _usersById.Add(user.Id, user);
            _rolesByUserId[user.Id] = [];
            CreateCount++;

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _usersById[user.Id] = user;

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _usersById.Remove(user.Id);
            _rolesByUserId.Remove(user.Id);

            return Task.FromResult(IdentityResult.Success);
        }

        public Task<string> GetUserIdAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Id);
        }

        public Task<string?> GetUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.UserName);
        }

        public Task SetUserNameAsync(
            ApplicationUser user,
            string? userName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.UserName = userName;

            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task SetNormalizedUserNameAsync(
            ApplicationUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedUserName = normalizedName;

            return Task.CompletedTask;
        }

        public Task<ApplicationUser?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _usersById.TryGetValue(userId, out var user);

            return Task.FromResult(user);
        }

        public Task<ApplicationUser?> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = _usersById.Values.SingleOrDefault(
                candidate => string.Equals(
                    candidate.NormalizedUserName,
                    normalizedUserName,
                    StringComparison.Ordinal));

            return Task.FromResult(user);
        }

        public Task SetEmailAsync(
            ApplicationUser user,
            string? email,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.Email = email;

            return Task.CompletedTask;
        }

        public Task<string?> GetEmailAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.EmailConfirmed);
        }

        public Task SetEmailConfirmedAsync(
            ApplicationUser user,
            bool confirmed,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.EmailConfirmed = confirmed;

            return Task.CompletedTask;
        }

        public Task<ApplicationUser?> FindByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = _usersById.Values.SingleOrDefault(
                candidate => string.Equals(
                    candidate.NormalizedEmail,
                    normalizedEmail,
                    StringComparison.Ordinal));

            return Task.FromResult(user);
        }

        public Task<string?> GetNormalizedEmailAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.NormalizedEmail);
        }

        public Task SetNormalizedEmailAsync(
            ApplicationUser user,
            string? normalizedEmail,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedEmail = normalizedEmail;

            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(
            ApplicationUser user,
            string? passwordHash,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PasswordHash = passwordHash;

            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }

        public Task AddToRoleAsync(
            ApplicationUser user,
            string roleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_rolesByUserId.TryGetValue(user.Id, out var roles))
            {
                roles = [];
                _rolesByUserId[user.Id] = roles;
            }

            roles.Add(roleName);

            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(
            ApplicationUser user,
            string roleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_rolesByUserId.TryGetValue(user.Id, out var roles))
            {
                roles.Remove(roleName);
            }

            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IList<string> roles = _rolesByUserId.TryGetValue(user.Id, out var userRoles)
                ? userRoles.ToList()
                : [];

            return Task.FromResult(roles);
        }

        public Task<bool> IsInRoleAsync(
            ApplicationUser user,
            string roleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isInRole = _rolesByUserId.TryGetValue(user.Id, out var roles) &&
                roles.Contains(roleName);

            return Task.FromResult(isInRole);
        }

        public Task<IList<ApplicationUser>> GetUsersInRoleAsync(
            string roleName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IList<ApplicationUser> users = _usersById.Values
                .Where(user => _rolesByUserId.TryGetValue(user.Id, out var roles) &&
                    roles.Contains(roleName))
                .ToList();

            return Task.FromResult(users);
        }

        public void Dispose()
        {
        }
    }
}
