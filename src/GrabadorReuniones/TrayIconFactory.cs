using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GrabadorReuniones;

internal static class TrayIconFactory
{
    public static Icon Create(bool recording)
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var fill = recording ? Color.FromArgb(200, 36, 36) : Color.FromArgb(11, 61, 92);
            using var brush = new SolidBrush(fill);
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var pen = new Pen(Color.White, 2.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            // Pequeña forma de onda
            var pts = new[]
            {
                new PointF(8, 16),
                new PointF(11, 11),
                new PointF(14, 21),
                new PointF(17, 9),
                new PointF(20, 20),
                new PointF(23, 13),
                new PointF(26, 16)
            };
            g.DrawLines(pen, pts);
        }

        var hicon = bmp.GetHicon();
        try
        {
            // Copiar para poder destruir el handle original.
            using var tmp = Icon.FromHandle(hicon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
            bmp.Dispose();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
