using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Один формат в ряду выбора: название и отметка «выбран»
/// </summary>
public sealed class FormatOptionViewModel : Bindable
{
    /// <summary>
    /// Выбран ли формат
    /// </summary>
    private bool _isSelected;

    /// <summary>
    /// Создаёт вариант
    /// </summary>
    /// <param name="format">Формат</param>
    /// <param name="isSelected">Выбран ли он сейчас</param>
    /// <param name="select">Вызывается по нажатию: параметр — формат</param>
    public FormatOptionViewModel(HomeworkFormatEnum format, bool isSelected, Action<HomeworkFormatEnum> select)
    {
        Format = format;
        Title = HomeworkFormats.Title(format);
        _isSelected = isSelected;
        SelectCommand = new RelayCommand(() => select(format));
    }

    /// <summary>
    /// Формат
    /// </summary>
    public HomeworkFormatEnum Format { get; }

    /// <summary>
    /// Название формата
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Выбран ли формат
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>
    /// Выбирает формат
    /// </summary>
    public RelayCommand SelectCommand { get; }
}