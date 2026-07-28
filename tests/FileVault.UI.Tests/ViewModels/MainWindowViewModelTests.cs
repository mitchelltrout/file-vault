using FileVault.Shared.Ipc.Messages;
using FileVault.UI.Tests.Ipc;
using FileVault.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileVault.UI.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel MakeVm(FakeServiceClient? client = null) =>
        new(client ?? new FakeServiceClient());

    [Fact]
    public void InitialState_HasNoVaults()
    {
        var vm = MakeVm();
        vm.Vaults.Should().BeEmpty();
        vm.ActiveVault.Should().BeNull();
    }

    [Fact]
    public async Task UnlockVault_AddsToListAndSetsActive()
    {
        var client = new FakeServiceClient();
        client.UnlockResponses["C:/test.vault"] = new UnlockVaultResponse { DisplayName = "My Vault" };
        var vm = MakeVm(client);

        await vm.UnlockVaultAsync("C:/test.vault", "password");

        vm.Vaults.Should().HaveCount(1);
        vm.Vaults[0].DisplayName.Should().Be("My Vault");
        vm.ActiveVault.Should().Be(vm.Vaults[0]);
    }

    [Fact]
    public async Task LockVault_RemovesFromActiveAndCallsService()
    {
        var client = new FakeServiceClient();
        client.UnlockResponses["C:/test.vault"] = new UnlockVaultResponse { DisplayName = "V" };
        var vm = MakeVm(client);
        await vm.UnlockVaultAsync("C:/test.vault", "pass");

        await vm.LockVaultAsync("C:/test.vault");

        vm.Vaults.Should().HaveCount(1);
        vm.Vaults[0].IsUnlocked.Should().BeFalse();
        vm.ActiveVault.Should().BeNull();
        client.Calls.Should().Contain("Lock:C:/test.vault");
    }

    [Fact]
    public async Task UnlockFailed_DoesNotAddVault()
    {
        var client = new FakeServiceClient { ThrowOn = new Exception("Wrong password") };
        var vm = MakeVm(client);

        var act = async () => await vm.UnlockVaultAsync("C:/bad.vault", "wrong");
        await act.Should().ThrowAsync<Exception>();
        vm.Vaults.Should().BeEmpty();
    }
}
