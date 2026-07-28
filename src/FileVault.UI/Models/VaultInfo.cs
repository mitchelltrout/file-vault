namespace FileVault.UI.Models;

public class VaultInfo
{
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsUnlocked { get; set; }

    public string Initial => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[0].ToString().ToUpperInvariant();
}
