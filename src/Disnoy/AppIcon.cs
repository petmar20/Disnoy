using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Disnoy
{
    // Draws the app icon: a display with a smiley on its screen.
    internal static class AppIcon
    {
        public static readonly Color WatchingColor = Color.FromArgb(47, 128, 237);
        public static readonly Color MissingColor = Color.FromArgb(242, 153, 74);
        public static readonly Color PausedColor = Color.FromArgb(138, 138, 138);

        private static readonly Dictionary<string, Icon> Cache = new Dictionary<string, Icon>();

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Color ColorFor(KeeperState state)
        {
            switch (state)
            {
                case KeeperState.DisplayMissing: return MissingColor;
                case KeeperState.Paused: return PausedColor;
                default: return WatchingColor;
            }
        }

        public static Icon IconFor(KeeperState state, int size)
        {
            string key = state + "@" + size;
            if (!Cache.TryGetValue(key, out Icon icon))
            {
                using (Bitmap bitmap = Draw(size, ColorFor(state)))
                {
                    IntPtr handle = bitmap.GetHicon();
                    icon = (Icon)Icon.FromHandle(handle).Clone();
                    DestroyIcon(handle);
                }
                Cache[key] = icon;
            }
            return icon;
        }

        public static Bitmap Draw(int size, Color color)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 32f, size / 32f);

                using (var fill = new SolidBrush(color))
                using (var screen = new SolidBrush(Color.White))
                using (var smile = new Pen(color, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    // Display with its stand.
                    FillRounded(g, fill, new RectangleF(1, 3, 30, 21), 4);
                    FillRounded(g, screen, new RectangleF(3.5f, 5.5f, 25, 16), 2);
                    g.FillRectangle(fill, 13, 23.5f, 6, 3.5f);
                    FillRounded(g, fill, new RectangleF(8, 26, 16, 3.5f), 1.7f);

                    // :) on the screen.
                    g.FillEllipse(fill, 9.6f, 8.6f, 4f, 4f);
                    g.FillEllipse(fill, 18.4f, 8.6f, 4f, 4f);
                    g.DrawArc(smile, 9.5f, 7.5f, 13f, 10.5f, 25, 130);
                }
            }
            return bitmap;
        }

        // Writes a multi-size .ico (PNG entries) for the exe. Run: Disnoy.exe --write-icon path
        public static void WriteIcoFile(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
            var images = new List<byte[]>();
            foreach (int size in sizes)
            {
                using (Bitmap bitmap = Draw(size, WatchingColor))
                using (var png = new MemoryStream())
                {
                    bitmap.Save(png, ImageFormat.Png);
                    images.Add(png.ToArray());
                }
            }

            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((short)0);            // reserved
                writer.Write((short)1);            // type: icon
                writer.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dimension = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    writer.Write(dimension);         // width
                    writer.Write(dimension);         // height
                    writer.Write((byte)0);           // palette colors
                    writer.Write((byte)0);           // reserved
                    writer.Write((short)1);          // color planes
                    writer.Write((short)32);         // bits per pixel
                    writer.Write(images[i].Length);
                    writer.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] image in images)
                {
                    writer.Write(image);
                }
            }
        }

        private static void FillRounded(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using (GraphicsPath path = Rounded(rect, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Blend(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }
    }
}
