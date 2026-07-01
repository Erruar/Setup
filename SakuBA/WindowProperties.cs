using System;
using System.Windows;

namespace SakuBA;

public class WindowProperties : DependencyObject
{
    public static readonly DependencyProperty IsLightBackgroundProperty = DependencyProperty.Register(
        nameof(IsLightBackground), typeof(bool), typeof(WindowProperties), new PropertyMetadata(false));

    private static readonly Lazy<WindowProperties> WindowInstance = new(() =>
    {
        var wp = new WindowProperties();
        wp.CheckBackgroundBrightness();
        return wp;
    });

    public static WindowProperties Instance => WindowInstance.Value;

    public bool IsLightBackground
    {
        get => (bool)GetValue(IsLightBackgroundProperty);
        private set => SetValue(IsLightBackgroundProperty, value);
    }

    private void CheckBackgroundBrightness()
    {
        var windowBrush = SystemColors.WindowBrush;
        var color = System.Drawing.Color.FromArgb(windowBrush.Color.A, windowBrush.Color.R, windowBrush.Color.G, windowBrush.Color.B);
        var brightness = color.GetBrightness();

        IsLightBackground = brightness > 0.7f;
    }
}