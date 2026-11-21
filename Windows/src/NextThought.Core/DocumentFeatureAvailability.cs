namespace NextThought.Core;

/// <summary>
/// Whether a document feature is actually usable (port of DocumentFeatureAvailability.swift):
/// availability includes every dependency, so choosing a provider never makes it ready.
/// </summary>
public sealed record DocumentFeatureAvailability(
    bool Checking,
    bool Runtime,
    bool Ocr,
    bool Layout,
    bool Embedding,
    bool CodexConnected)
{
    public enum Feature
    {
        Paddle,
        Codex,
        Semantic,
        Office
    }

    public bool Ready(Feature feature)
    {
        if (Checking) return false;
        return feature switch
        {
            Feature.Paddle => Runtime && Ocr && Layout,
            Feature.Codex => Runtime && CodexConnected,
            Feature.Semantic => Runtime && Embedding,
            Feature.Office => Runtime,
            _ => false
        };
    }
}
