using System.Windows;
using System.Windows.Media;

namespace FramePlay.Themes;

/// <summary>
/// FramePlay's solid icon set (24x24). Shapes with an inside are filled; line icons (shuffle, list, corners)
/// are drawn from centre lines with a 2 px round-capped pen and turned into fill geometry here, so every icon
/// has the same weight and works with the Fill-only icon templates.
/// </summary>
public static class IconFactory
{
    private const string Speaker = "M3.5,9.25 Q3.5,8.5 4.25,8.5 L7,8.5 L11.1,5.1 Q12,4.4 12,5.5 L12,18.5 Q12,19.6 11.1,18.9 L7,15.5 L4.25,15.5 Q3.5,15.5 3.5,14.75 Z";

    public static void Register(ResourceDictionary res)
    {
        Add(res, "Icon.VolumeFill", Speaker, "M15.05,9.45 A3.6,3.6 0 0 1 15.05,14.55 M17.18,6.8 A7,7 0 0 1 17.18,17.2");
        Add(res, "Icon.VolumeMuteFill", Speaker, "M15.5,9.5 L20,14 M20,9.5 L15.5,14");
        Add(res, "Icon.ShuffleFill",
            "M21.6,7 L18.1,4.3 L18.1,9.7 Z M21.6,17 L18.1,14.3 L18.1,19.7 Z",
            "M3,7 L6.5,7 C9.5,7 10.5,8.5 12,12 C13.5,15.5 14.5,17 17.5,17 L18.5,17 M3,17 L6.5,17 C8,17 9,16.4 10,15.2 M14,8.8 C15,7.5 16,7 17.5,7 L18.5,7");
        Add(res, "Icon.AudioFill", null, "M4,10 L4,14 M8,7 L8,17 M12,4.5 L12,19.5 M16,8 L16,16 M20,10 L20,14", 2.4);
        Add(res, "Icon.QueueFill",
            "M3.5,7 A1.6,1.6 0 1 0 6.7,7 A1.6,1.6 0 1 0 3.5,7 Z M3.5,12 A1.6,1.6 0 1 0 6.7,12 A1.6,1.6 0 1 0 3.5,12 Z M3.5,17 A1.6,1.6 0 1 0 6.7,17 A1.6,1.6 0 1 0 3.5,17 Z",
            "M10,7 L20.5,7 M10,12 L20.5,12 M10,17 L20.5,17");
        Add(res, "Icon.PipFill",
            "M14.5,13 L20,13 Q21.5,13 21.5,14.5 L21.5,18 Q21.5,19.5 20,19.5 L14.5,19.5 Q13,19.5 13,18 L13,14.5 Q13,13 14.5,13 Z",
            "M10,19 L5,19 Q3,19 3,17 L3,7 Q3,5 5,5 L19,5 Q21,5 21,7 L21,10");
        Add(res, "Icon.FullscreenFill", null, "M4,9 L4,4 L9,4 M15,4 L20,4 L20,9 M20,15 L20,20 L15,20 M9,20 L4,20 L4,15", 2.2);
        Add(res, "Icon.ExitFullscreenFill", null, "M9,4 L9,9 L4,9 M15,4 L15,9 L20,9 M20,15 L15,15 L15,20 M4,15 L9,15 L9,20", 2.2);
        Add(res, "Icon.SortFill", null, "M8,5 L8,19 M5,16 L8,19.2 L11,16 M16,19 L16,5 M13,8 L16,4.8 L19,8");
        Add(res, "Icon.MinusFill", null, "M5,12 L19,12");
        Add(res, "Icon.PlusFill", null, "M5,12 L19,12 M12,5 L12,19");
        Add(res, "Icon.OpenFill", null, "M10,5 L6.5,5 Q4.5,5 4.5,7 L4.5,17.5 Q4.5,19.5 6.5,19.5 L17,19.5 Q19,19.5 19,17.5 L19,14 M13.5,5 L19,5 L19,10.5 M19,5 L11.5,12.5");
        Add(res, "Icon.CloseBold", null, "M6.5,6.5 L17.5,17.5 M17.5,6.5 L6.5,17.5");
        Add(res, "Icon.FolderFill", "M3,7.5 Q3,5.5 5,5.5 L9.2,5.5 Q10,5.5 10.5,6.1 L11.6,7.5 L19,7.5 Q21,7.5 21,9.5 L21,17 Q21,19 19,19 L5,19 Q3,19 3,17 Z", null);
        // Solid box with the subtitle lines cut out.
        Raw(res, "Icon.SubtitlesFill", "F0 M5,5 L19,5 Q21.5,5 21.5,7.5 L21.5,16.5 Q21.5,19 19,19 L5,19 Q2.5,19 2.5,16.5 L2.5,7.5 Q2.5,5 5,5 Z " +
            "M6.75,13.75 L10.75,13.75 Q11.5,13.75 11.5,14.5 Q11.5,15.25 10.75,15.25 L6.75,15.25 Q6,15.25 6,14.5 Q6,13.75 6.75,13.75 Z " +
            "M13.25,13.75 L17.25,13.75 Q18,13.75 18,14.5 Q18,15.25 17.25,15.25 L13.25,15.25 Q12.5,15.25 12.5,14.5 Q12.5,13.75 13.25,13.75 Z " +
            "M6.75,9.75 L8.75,9.75 Q9.5,9.75 9.5,10.5 Q9.5,11.25 8.75,11.25 L6.75,11.25 Q6,11.25 6,10.5 Q6,9.75 6.75,9.75 Z " +
            "M11.25,9.75 L17.25,9.75 Q18,9.75 18,10.5 Q18,11.25 17.25,11.25 L11.25,11.25 Q10.5,11.25 10.5,10.5 Q10.5,9.75 11.25,9.75 Z");
        // Speed gauge: thick arc, needle and hub.
        Raw(res, "Icon.SpeedFill", "F1 M3.54,17.08 A9,9 0 1 1 20.46,17.08 L18.39,16.33 A6.8,6.8 0 1 0 5.61,16.33 Z " +
            "M16.5,8.64 L13,14.84 L11,13.16 Z M10,14 A2,2 0 1 0 14,14 A2,2 0 1 0 10,14 Z");
        res["Icon.GearFill"] = Gear();
        Raw(res, "Icon.EditFill", "F1 M15.2,3.8 Q16.2,2.8 17.2,3.8 L20.2,6.8 Q21.2,7.8 20.2,8.8 L9,20 L4,20 L4,15 Z");
        // Music note for files without cover art.
        Raw(res, "Icon.NoteFill", "F1 M3,17.5 A3.25,3.25 0 1 0 9.5,17.5 A3.25,3.25 0 1 0 3,17.5 Z M13,15.5 A3.25,3.25 0 1 0 19.5,15.5 A3.25,3.25 0 1 0 13,15.5 Z " +
            "M8.25,7.5 L9.75,7.5 L9.75,17.5 L8.25,17.5 Z M18.25,5 L19.75,5 L19.75,15.5 L18.25,15.5 Z M8.25,6.1 L19.75,3.5 L19.75,5.9 L8.25,8.5 Z");
    }

    private static void Add(ResourceDictionary res, string key, string? fill, string? strokes, double width = 2.0)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        if (fill != null) group.Children.Add(Geometry.Parse(fill));
        if (strokes != null)
        {
            var pen = new Pen(Brushes.Black, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            var widened = Geometry.Parse(strokes).GetWidenedPathGeometry(pen);
            widened.FillRule = FillRule.Nonzero;
            group.Children.Add(widened);
        }
        group.Freeze();
        res[key] = group;
    }

    /// <summary>A solid 8-tooth gear with a round hole.</summary>
    private static Geometry Gear()
    {
        static Point P(double deg, double r)
        {
            double a = deg * Math.PI / 180;
            return new Point(12 + r * Math.Cos(a), 12 + r * Math.Sin(a));
        }
        var g = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var c = g.Open())
        {
            bool first = true;
            for (int i = 0; i < 8; i++)
            {
                double a = i * 45 - 90;
                foreach (var (d, r) in new[] { (a - 17, 7.4), (a - 9, 10.0), (a + 9, 10.0), (a + 17, 7.4) })
                {
                    if (first) { c.BeginFigure(P(d, r), true, true); first = false; }
                    else c.LineTo(P(d, r), true, true);
                }
            }
            c.BeginFigure(new Point(8.8, 12), true, true);
            c.ArcTo(new Point(15.2, 12), new Size(3.2, 3.2), 0, false, SweepDirection.Clockwise, true, false);
            c.ArcTo(new Point(8.8, 12), new Size(3.2, 3.2), 0, false, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private static void Raw(ResourceDictionary res, string key, string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        res[key] = g;
    }
}
