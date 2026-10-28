namespace Steward.ViewModels;

/// <summary>
/// Группа плиток состояния одного процесса на странице «Статус»
/// </summary>
/// <param name="Title">Название процесса</param>
/// <param name="Tiles">Плитки группы</param>
public sealed record StatusGroupViewModel(string Title, IReadOnlyList<StatusTileViewModel> Tiles);
