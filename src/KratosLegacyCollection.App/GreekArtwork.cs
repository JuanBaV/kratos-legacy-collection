using System.Windows;
using System.Windows.Media;

namespace KratosLegacyCollection.App;

// Original architectural geometry. No game artwork, logo or character likeness is used.
public sealed class GreekArtwork : FrameworkElement
{
    public static readonly DependencyProperty ChapterProperty = DependencyProperty.Register(nameof(Chapter), typeof(string), typeof(GreekArtwork), new FrameworkPropertyMetadata("I", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Chapter { get => (string)GetValue(ChapterProperty); set => SetValue(ChapterProperty, value); }
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);
        var width = ActualWidth; var height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(44, 32, 27), Color.FromRgb(17, 17, 17), 80), null, new(0, 0, width, height));
        var bronze = new SolidColorBrush(Color.FromRgb(115, 90, 60));
        var stone = new SolidColorBrush(Color.FromRgb(70, 63, 53));
        var edge = new Pen(new SolidColorBrush(Color.FromRgb(155, 122, 81)), 1);
        var center = width * .48; var baseline = height * .82; var templeWidth = Math.Min(width * .66, height * 1.5); var columnHeight = height * .43;
        for (var i = 0; i < 4; i++) dc.DrawRectangle(stone, null, new(center - templeWidth / 2 - i * 5, baseline + i * 4, templeWidth + i * 10, 3));
        for (var i = 0; i < 6; i++) {
            var x = center - templeWidth / 2 + templeWidth * (i + .5) / 6;
            var broken = Chapter is "V" or "VI" && i % 3 == 0;
            var top = baseline - columnHeight + (broken ? columnHeight * .38 : 0);
            dc.DrawRectangle(stone, edge, new(x - 6, top, 12, baseline - top));
            if (!broken) { dc.DrawRectangle(bronze, null, new(x - 10, top - 4, 20, 4)); dc.DrawRectangle(bronze, null, new(x - 8, baseline - 3, 16, 3)); }
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(40, 35, 30)), 2), new(x, top + 3), new(x, baseline - 5));
        }
        var roof = new StreamGeometry();
        using (var ctx = roof.Open()) { ctx.BeginFigure(new(center - templeWidth / 2 - 12, baseline - columnHeight - 8), true, true); ctx.LineTo(new(center, baseline - columnHeight - height * .2), true, false); ctx.LineTo(new(center + templeWidth / 2 + 12, baseline - columnHeight - 8), true, false); }
        dc.DrawGeometry(stone, edge, roof);
        dc.DrawLine(new Pen(bronze, 3), new(0, height - 8), new(width, height - 8));
        for (int i = 0; i < width / 26; i++) {
            double x = i * 26;
            dc.DrawLine(edge, new(x, height - 2), new(x, height - 7)); dc.DrawLine(edge, new(x, height - 2), new(x + 17, height - 2)); dc.DrawLine(edge, new(x + 17, height - 2), new(x + 17, height - 5));
        }
        for (int i = 0; i < 9; i++) {
            var x = width * ((i * .173 + Chapter.Length * .11) % 1); var y = height * ((i * .271 + .07) % .85);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 191, 98, 47)), null, new(x, y), 1.2, 1.2);
        }
    }
}
