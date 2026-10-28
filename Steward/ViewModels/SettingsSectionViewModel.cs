using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Раздел страницы настроек: заголовок, поля и, если нужно, кнопка действия под ними
/// </summary>
public sealed class SettingsSectionViewModel
{
    /// <summary>
    /// Создаёт раздел
    /// </summary>
    /// <param name="title">Заголовок раздела</param>
    /// <param name="fields">Поля раздела</param>
    /// <param name="actionTitle">Подпись кнопки действия. Пусто, если кнопки нет</param>
    /// <param name="actionHint">Пояснение рядом с кнопкой</param>
    /// <param name="action">Действие кнопки</param>
    public SettingsSectionViewModel(string title, IReadOnlyList<SettingFieldViewModel> fields, string actionTitle = "", string actionHint = "", Action? action = null)
    {
        Title = title;
        Fields = fields;
        ActionTitle = actionTitle;
        ActionHint = actionHint;
        ActionCommand = new RelayCommand(() => action?.Invoke());
    }

    /// <summary>
    /// Заголовок раздела
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Поля раздела
    /// </summary>
    public IReadOnlyList<SettingFieldViewModel> Fields { get; }

    /// <summary>
    /// Подпись кнопки действия
    /// </summary>
    public string ActionTitle { get; }

    /// <summary>
    /// Пояснение рядом с кнопкой
    /// </summary>
    public string ActionHint { get; }

    /// <summary>
    /// В разделе есть кнопка действия
    /// </summary>
    public bool HasAction => ActionTitle.Length > 0;

    /// <summary>
    /// Действие кнопки
    /// </summary>
    public RelayCommand ActionCommand { get; }
}
