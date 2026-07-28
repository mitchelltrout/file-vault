using FileVault.Service.Crypto;
using FileVault.Service.VaultOperations;
using FluentAssertions;

namespace FileVault.Service.Tests.VaultOperations;

public class VaultManagerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private VaultManager? _manager;

    public VaultManagerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        _manager?.Dispose();
        Directory.Delete(_tempDir, recursive: true);
    }

    private string VaultPath => Path.Combine(_tempDir, "test.vault");

    [Fact]
    public async Task Create_ProducesVaultFile()
    {
        _manager = new VaultManager();
        await _manager.CreateVaultAsync(VaultPath, "My Vault", "password123", KeyDerivation.FastParams);
        File.Exists(VaultPath).Should().BeTrue();
    }

    [Fact]
    public async Task Unlock_AfterCreate_Succeeds()
    {
        _manager = new VaultManager();
        await _manager.CreateVaultAsync(VaultPath, "My Vault", "password123", KeyDerivation.FastParams);
        var session = await _manager.UnlockAsync(VaultPath, "password123", KeyDerivation.FastParams);
        session.Should().NotBeNull();
        session.DisplayName.Should().Be("My Vault");
    }

    [Fact]
    public async Task Unlock_WrongPassword_Throws()
    {
        _manager = new VaultManager();
        await _manager.CreateVaultAsync(VaultPath, "My Vault", "password123", KeyDerivation.FastParams);
        var act = async () => await _manager.UnlockAsync(VaultPath, "wrongpassword", KeyDerivation.FastParams);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Lock_DisposesSessionAndRejectsOperations()
    {
        _manager = new VaultManager();
        await _manager.CreateVaultAsync(VaultPath, "Vault", "pass", KeyDerivation.FastParams);
        var session = await _manager.UnlockAsync(VaultPath, "pass", KeyDerivation.FastParams);
        _manager.Lock(VaultPath);
        _manager.TryGetSession(VaultPath, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_AllowsUnlockWithNewPassword()
    {
        _manager = new VaultManager();
        await _manager.CreateVaultAsync(VaultPath, "Vault", "oldpass", KeyDerivation.FastParams);
        var session = await _manager.UnlockAsync(VaultPath, "oldpass", KeyDerivation.FastParams);
        await _manager.ChangePasswordAsync(VaultPath, "oldpass", "newpass", KeyDerivation.FastParams);

        var act = async () => await _manager.UnlockAsync(VaultPath, "oldpass", KeyDerivation.FastParams);
        await act.Should().ThrowAsync<Exception>();
        var newSession = await _manager.UnlockAsync(VaultPath, "newpass", KeyDerivation.FastParams);
        newSession.Should().NotBeNull();
    }
}
