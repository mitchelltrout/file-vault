using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileVault.UI.Ipc;
using System.Security.Cryptography;
using System.Text;

namespace FileVault.UI.ViewModels;

public enum GeneratorMode { Random, Memorable }

public partial class PasswordDialogViewModel(IServiceClient client, string vaultPath) : ObservableObject
{
    [ObservableProperty] private string _currentPassword = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmPassword = "";

    [ObservableProperty] private GeneratorMode _generatorMode = GeneratorMode.Random;
    [ObservableProperty] private string _generatedPassword = "";
    [ObservableProperty] private string _entropyDescription = "";

    [ObservableProperty] private int _randomLength = 24;
    [ObservableProperty] private bool _includeUppercase = true;
    [ObservableProperty] private bool _includeLowercase = true;
    [ObservableProperty] private bool _includeNumbers = true;
    [ObservableProperty] private bool _includeSymbols = true;
    [ObservableProperty] private bool _excludeAmbiguous = false;

    [ObservableProperty] private int _wordPairs = 1;
    [ObservableProperty] private string _memorableSeparator = "$";
    [ObservableProperty] private int _digitSuffixLength = 4;
    [ObservableProperty] private bool _capitalizeWords = true;

    private static readonly string[] Adjectives = [
        "Silent", "Brave", "Swift", "Dark", "Bright", "Bold", "Calm", "Cold",
        "Deep", "Fast", "Firm", "Free", "Gold", "Hard", "High", "Hot", "Kind",
        "Long", "Loud", "Mild", "Near", "Nice", "Pure", "Rare", "Rich", "Safe",
        "Soft", "Tall", "Thin", "True", "Vast", "Warm", "Wild", "Wise", "Young"
    ];

    private static readonly string[] Nouns = [
        "River", "Stone", "Tower", "Cloud", "Field", "Flame", "Frost", "Grove",
        "Haven", "Hedge", "Light", "Maple", "Ocean", "Plain", "Ridge", "Shade",
        "Shore", "Spark", "Storm", "Trail", "Vault", "Voice", "Cliff", "Creek",
        "Delta", "Ember", "Forge", "Haven", "Lunar", "Marsh", "Nexus", "Orbit"
    ];

    [RelayCommand]
    private void Generate()
    {
        GeneratedPassword = GeneratorMode == GeneratorMode.Random
            ? GenerateRandom()
            : GenerateMemorable();
        EntropyDescription = CalculateEntropy(GeneratedPassword, GeneratorMode);
    }

    private string GenerateRandom()
    {
        var chars = new StringBuilder();
        if (IncludeUppercase) chars.Append("ABCDEFGHJKLMNPQRSTUVWXYZ");
        if (IncludeLowercase) chars.Append("abcdefghjkmnpqrstuvwxyz");
        if (IncludeNumbers) chars.Append("23456789");
        if (IncludeSymbols) chars.Append("!@#$%^&*");

        if (!ExcludeAmbiguous)
        {
            if (IncludeUppercase) chars.Append("IO");
            if (IncludeLowercase) chars.Append("il");
            if (IncludeNumbers) chars.Append("01");
        }

        if (chars.Length == 0) chars.Append("abcdefghijklmnopqrstuvwxyz");
        var pool = chars.ToString();
        var result = new char[RandomLength];
        for (int i = 0; i < RandomLength; i++)
            result[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        return new string(result);
    }

    private string GenerateMemorable()
    {
        var parts = new List<string>();
        for (int i = 0; i < WordPairs; i++)
        {
            var adj = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
            var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
            if (!CapitalizeWords) { adj = adj.ToLower(); noun = noun.ToLower(); }
            parts.Add(adj);
            parts.Add(noun);
        }

        if (DigitSuffixLength > 0)
        {
            var max = (int)Math.Pow(10, DigitSuffixLength);
            parts.Add(RandomNumberGenerator.GetInt32(max).ToString($"D{DigitSuffixLength}"));
        }

        return string.Join(MemorableSeparator, parts);
    }

    private static string CalculateEntropy(string password, GeneratorMode mode)
    {
        int poolSize = 0;
        if (password.Any(char.IsUpper)) poolSize += 26;
        if (password.Any(char.IsLower)) poolSize += 26;
        if (password.Any(char.IsDigit)) poolSize += 10;
        if (password.Any(c => "!@#$%^&*$-_.#".Contains(c))) poolSize += 20;
        if (poolSize == 0) return "";

        double entropy = password.Length * Math.Log2(poolSize);
        var bits = (int)entropy;
        var centuries = Math.Pow(10, (entropy / Math.Log2(10)) - 9) / (3.15e7);
        var timeStr = centuries > 1e6 ? "centuries" : centuries > 1 ? $"~{centuries:F0} centuries" : "< 1 century";
        return $"~{bits} bits — {timeStr} at 1B guesses/sec";
    }

    public async Task SubmitAsync()
    {
        if (NewPassword != ConfirmPassword)
            throw new InvalidOperationException("Passwords do not match.");
        await client.ChangePasswordAsync(vaultPath, CurrentPassword, NewPassword);
    }
}
