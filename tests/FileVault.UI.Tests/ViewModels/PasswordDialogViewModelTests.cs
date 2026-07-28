using FileVault.UI.Tests.Ipc;
using FileVault.UI.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileVault.UI.Tests.ViewModels;

public class PasswordDialogViewModelTests
{
    [Fact]
    public void GenerateRandom_RespectsLength()
    {
        var vm = new PasswordDialogViewModel(new FakeServiceClient(), "C:/v.vault");
        vm.GeneratorMode = GeneratorMode.Random;
        vm.RandomLength = 20;
        vm.GenerateCommand.Execute(null);
        vm.GeneratedPassword.Should().HaveLength(20);
    }

    [Fact]
    public void GenerateRandom_NumbersOnly_ContainsOnlyDigits()
    {
        var vm = new PasswordDialogViewModel(new FakeServiceClient(), "C:/v.vault");
        vm.GeneratorMode = GeneratorMode.Random;
        vm.IncludeUppercase = false;
        vm.IncludeLowercase = false;
        vm.IncludeSymbols = false;
        vm.IncludeNumbers = true;
        vm.GenerateCommand.Execute(null);
        vm.GeneratedPassword.Should().MatchRegex("^[0-9]+$");
    }

    [Fact]
    public void GenerateMemorable_ContainsSeparator()
    {
        var vm = new PasswordDialogViewModel(new FakeServiceClient(), "C:/v.vault");
        vm.GeneratorMode = GeneratorMode.Memorable;
        vm.MemorableSeparator = "$";
        vm.GenerateCommand.Execute(null);
        vm.GeneratedPassword.Should().Contain("$");
    }

    [Fact]
    public void GenerateRandom_EntropyIsNonZero()
    {
        var vm = new PasswordDialogViewModel(new FakeServiceClient(), "C:/v.vault");
        vm.GenerateCommand.Execute(null);
        vm.EntropyDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChangePassword_CallsService()
    {
        var client = new FakeServiceClient();
        var vm = new PasswordDialogViewModel(client, "C:/v.vault");
        vm.CurrentPassword = "oldpass";
        vm.NewPassword = "newpass";
        vm.ConfirmPassword = "newpass";

        await vm.SubmitAsync();

        client.Calls.Should().Contain("ChangePassword:C:/v.vault");
    }

    [Fact]
    public async Task ChangePassword_MismatchedConfirm_ThrowsValidation()
    {
        var vm = new PasswordDialogViewModel(new FakeServiceClient(), "C:/v.vault");
        vm.CurrentPassword = "old";
        vm.NewPassword = "new1";
        vm.ConfirmPassword = "new2";

        var act = async () => await vm.SubmitAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
