using System.Windows;
using System.Windows.Threading;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Состояние переворота листа календаря: старый лист сворачивается, на середине подменяется содержимое, новый разворачивается. Фазы отдаёт разметке, а она играет анимацию
/// </summary>
public sealed class FlipState : Bindable
{
    /// <summary>
    /// Сколько миллисекунд лист сворачивается
    /// </summary>
    public const int OUT_MILLISECONDS = 140;

    /// <summary>
    /// Сколько миллисекунд лист разворачивается
    /// </summary>
    public const int IN_MILLISECONDS = 240;

    /// <summary>
    /// Фаза: 0 покой, 1 лист сворачивается, 2 лист разворачивается
    /// </summary>
    private int _phase;

    /// <summary>
    /// Направление: 1 вперёд, минус один назад
    /// </summary>
    private int _direction = 1;

    /// <summary>
    /// Фаза: 0 покой, 1 лист сворачивается, 2 лист разворачивается
    /// </summary>
    public int Phase
    {
        get => _phase;
        private set => Set(ref _phase, value);
    }

    /// <summary>
    /// Направление: 1 вперёд, минус один назад
    /// </summary>
    public int Direction
    {
        get => _direction;
        private set => Set(ref _direction, value);
    }

    /// <summary>
    /// Выполняет смену содержимого с переворотом. Без окна (в тестах) и во время другого переворота смена идёт сразу, без анимации, чтобы быстрые нажатия не терялись
    /// </summary>
    /// <param name="direction">Куда листаем: 1 вперёд, минус один назад</param>
    /// <param name="change">Смена содержимого листа</param>
    public void Run(int direction, Action change)
    {
        if (System.Windows.Application.Current is null || Phase != 0)
        {
            change();

            return;
        }

        Direction = direction >= 0 ? 1 : -1;
        Phase = 1;
        After(OUT_MILLISECONDS, () =>
        {
            change();
            Phase = 2;
            After(IN_MILLISECONDS, () => Phase = 0);
        });
    }

    /// <summary>
    /// Выполняет действие в потоке окна через заданное время
    /// </summary>
    /// <param name="milliseconds">Через сколько миллисекунд</param>
    /// <param name="action">Действие</param>
    private static void After(int milliseconds, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }
}
