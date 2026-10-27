namespace Steward.Reels.Models;

/// <summary>
/// Разбор идеи, который сделал Claude
/// </summary>
/// <param name="Title">Название идеи, понятное без рилса</param>
/// <param name="Summary">Краткий разбор: суть, для чего, плюсы и минусы</param>
public sealed record IdeaAnalysis(string Title, string Summary);
