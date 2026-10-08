using System.Windows;
using System.Windows.Controls;
using materials.Services;

namespace materials
{
    /// <summary>一排可點的小方塊（預設單位、最近用過的用途 / 借用人），點一下就填入。</summary>
    internal static class Chips
    {
        /// <param name="caption">放在最前面的灰色小字（例如「最近：」）；null 表示不放。</param>
        /// <param name="highlighted">與這個值同名的方塊以強調色標示。</param>
        public static void Fill(WrapPanel panel, IEnumerable<string> values, Action<string> onClick,
            string? caption = null, string? highlighted = null, bool small = false)
        {
            panel.Children.Clear();
            foreach (var value in values)
            {
                var chip = new Button
                {
                    Content = value,
                    MinWidth = small ? 0 : 44,
                    MaxWidth = 220,
                    FontSize = small ? 12 : 13,
                    Padding = small ? new Thickness(9, 3, 9, 3) : new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 6, 6),
                    ToolTip = value,
                    Style = (Style)panel.FindResource(CategoryService.SameName(value, highlighted) ? "PrimaryButton" : "BaseButton"),
                };
                chip.Click += (_, _) => onClick(value);
                panel.Children.Add(chip);
            }
            if (caption != null && panel.Children.Count > 0)
            {
                var label = new TextBlock { Text = caption, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 6) };
                label.SetResourceReference(TextBlock.ForegroundProperty, "SubtleBrush");
                panel.Children.Insert(0, label);
            }
            panel.Visibility = panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
