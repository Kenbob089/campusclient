using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CampusMidnight
{
    // pan, zoom and rotate a picture under a fixed frame, then save what is inside the frame
    class CropCanvas : Control
    {
        Bitmap img;
        readonly bool circle;
        float zoom = 1f;
        PointF off = PointF.Empty;
        Point last;
        bool drag;

        public CropCanvas(Bitmap source, bool round)
        {
            img = source;
            circle = round;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.SizeAll;
        }

        RectangleF Region()
        {
            float m = Math.Min(Width, Height) * 0.08f;
            float s = Math.Min(Width, Height) - m * 2;
            return new RectangleF((Width - s) / 2f, (Height - s) / 2f, s, s);
        }

        float Scale()
        {
            RectangleF r = Region();
            return Math.Max(r.Width / img.Width, r.Height / img.Height) * zoom;
        }

        void Clamp()
        {
            RectangleF r = Region();
            float k = Scale();
            float mx = Math.Max(0, (img.Width * k - r.Width) / 2f);
            float my = Math.Max(0, (img.Height * k - r.Height) / 2f);
            off.X = Math.Max(-mx, Math.Min(mx, off.X));
            off.Y = Math.Max(-my, Math.Min(my, off.Y));
        }

        public void ZoomBy(float f)
        {
            zoom = Math.Max(1f, Math.Min(8f, zoom * f));
            Clamp(); Invalidate();
        }

        public void Rotate()
        {
            Bitmap n = (Bitmap)img.Clone();
            n.RotateFlip(RotateFlipType.Rotate90FlipNone);
            img.Dispose();
            img = n;
            zoom = 1f; off = PointF.Empty;
            Invalidate();
        }

        public void ResetView() { zoom = 1f; off = PointF.Empty; Invalidate(); }

        void Paint(Graphics g, RectangleF dest, RectangleF region)
        {
            float k = Scale();
            float f = dest.Width / region.Width;
            float w = img.Width * k, h = img.Height * k;
            float cx = region.X + region.Width / 2f + off.X, cy = region.Y + region.Height / 2f + off.Y;
            RectangleF ir = new RectangleF(dest.X + (cx - w / 2f - region.X) * f, dest.Y + (cy - h / 2f - region.Y) * f, w * f, h * f);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, ir);
        }

        GraphicsPath Shape(RectangleF r)
        {
            GraphicsPath p = new GraphicsPath();
            if (circle) p.AddEllipse(r);
            else
            {
                float d = r.Width * 0.22f;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
            }
            return p;
        }

        public Bitmap Render(int size)
        {
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                RectangleF dest = new RectangleF(0, 0, size, size);
                if (!circle)
                {
                    using (GraphicsPath p = Shape(dest)) g.SetClip(p);
                }
                Paint(g, dest, Region());
            }
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = Region();
            Paint(g, r, r);
            // dim everything outside the frame
            using (GraphicsPath outer = new GraphicsPath())
            {
                outer.AddRectangle(new RectangleF(0, 0, Width, Height));
                outer.AddPath(Shape(r), false);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(190, Theme.Bg1))) g.FillPath(b, outer);
            }
            using (GraphicsPath sp = Shape(r))
            using (Pen p = new Pen(Theme.AccText, 2f)) g.DrawPath(p, sp);
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; last = e.Location; Capture = true; base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; base.OnMouseUp(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag)
            {
                off.X += e.X - last.X; off.Y += e.Y - last.Y;
                last = e.Location;
                Clamp(); Invalidate();
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ZoomBy((float)Math.Pow(1.1, e.Delta / 120.0));
            base.OnMouseWheel(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && img != null) img.Dispose();
            base.Dispose(disposing);
        }
    }

    class CropForm : DarkForm
    {
        public Bitmap Result;
        readonly CropCanvas canvas;

        public CropForm(Bitmap source, bool round, string title, string hint) : base(title)
        {
            int hMax = Screen.PrimaryScreen.WorkingArea.Height - Theme.S(90);
            ClientSize = new Size(Theme.S(520), Math.Min(Theme.S(760), hMax));
            Fw = ClientSize.Width - Theme.S(56) - SystemInformation.VerticalScrollBarWidth;
            Wrapped(hint, Theme.Body, Theme.T2);
            Gap(8);
            canvas = new CropCanvas(source, round);
            Add(canvas, Fw);
            Gap(14);
            PillButton zo = new PillButton(); zo.Text = "Zoom out";
            zo.Click += delegate { canvas.ZoomBy(0.85f); };
            PillButton zi = new PillButton(); zi.Text = "Zoom in";
            zi.Click += delegate { canvas.ZoomBy(1.18f); };
            PillButton ro = new PillButton(); ro.Text = "Rotate";
            ro.Click += delegate { canvas.Rotate(); };
            PillButton rs = new PillButton(); rs.Text = "Reset";
            rs.Click += delegate { canvas.ResetView(); };
            Buttons(zo, zi, ro, rs);
            PillButton ok = new PillButton(); ok.Text = "Save"; ok.Kind = 1;
            ok.Click += delegate { Result = canvas.Render(256); DialogResult = DialogResult.OK; Close(); };
            PillButton no = new PillButton(); no.Text = "Cancel";
            no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Buttons(ok, no);
            Finish();
        }
    }
}
