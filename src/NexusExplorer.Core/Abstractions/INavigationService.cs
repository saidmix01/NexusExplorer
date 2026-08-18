namespace NexusExplorer.Core.Abstractions;

/// <summary>
/// Abstraction for navigation within the file explorer.
/// </summary>
public interface INavigationService
{
    string CurrentPath { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    bool CanGoUp { get; }

    void NavigateTo(string path);
    void GoBack();
    void GoForward();
    void GoUp();
    void Refresh();

    event EventHandler<string>? Navigated;
}
