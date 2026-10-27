using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Одна кнопка выбора отметки предмета: «Ждёт решения», «Расшифровывать», «Не нужно» или «Завершён»
/// </summary>
public sealed class LessonStateOptionViewModel : Bindable
{
    /// <summary>
    /// Эта отметка выбрана для занятия
    /// </summary>
    private bool _isSelected;

    /// <summary>
    /// Создаёт кнопку
    /// </summary>
    /// <param name="state">Отметка, которую ставит кнопка</param>
    /// <param name="isSelected">Выбрана ли она сейчас</param>
    /// <param name="select">Вызывается по нажатию: параметр — нажатая отметка</param>
    public LessonStateOptionViewModel(LessonStateEnum state, bool isSelected, Action<LessonStateEnum> select)
    {
        State = state;
        Title = TitleOf(state);
        _isSelected = isSelected;
        SelectCommand = new RelayCommand(() => select(state));
    }

    /// <summary>
    /// Отметка, которую ставит кнопка
    /// </summary>
    public LessonStateEnum State { get; }

    /// <summary>
    /// Подпись на кнопке
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Эта отметка выбрана для занятия
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>
    /// Ставит отметку
    /// </summary>
    public RelayCommand SelectCommand { get; }

    /// <summary>
    /// Подпись отметки для человека
    /// </summary>
    /// <param name="state">Отметка</param>
    /// <returns>Короткая подпись на русском</returns>
    public static string TitleOf(LessonStateEnum state) => state switch
    {
        LessonStateEnum.Fetch => "Расшифровывать",
        LessonStateEnum.Skip => "Не нужно",
        LessonStateEnum.Done => "Завершён",
        _ => "Ждёт решения",
    };
}
