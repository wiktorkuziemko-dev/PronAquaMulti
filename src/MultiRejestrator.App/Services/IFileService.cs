namespace MultiRejestrator.App.Services;

/// <summary>Okna wyboru plików – implementowane przez widok, aby modele widoków nie zależały od Avalonii.</summary>
public interface IFileService
{
    Task<string?> PickOpenAsync(string title, string? folder);

    Task<string?> PickSaveAsync(string title, string suggestedName, string filterName, string pattern, string? folder);
}
