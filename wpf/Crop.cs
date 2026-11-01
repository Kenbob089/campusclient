using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CampusMidnight
{
    // pan, zoom and rotate a picture under a fixed frame, then save what is inside the frame
    sealed class CropCanvas : FrameworkElement
    {
        BitmapSource img;
        readonly bool circle;
        double zoom = 1;
        Vector off = new Vector(0, 0);
        Point last;
        bool drag;

        public CropCanvas(BitmapSource source, bool round)
        {
            img = source;
            circle = round;
            Cursor = Cursors.SizeAll;
            ClipToBounds = true;
            Focusable = false;
        }

        Rect Region()
        {
            double m = Math.Min(ActualWidth, ActualHeight) * 0.08;
            double s = Math.Min(ActualWidth, ActualHeight) - m * 2;
            return new Rect((ActualWidth - s) / 2, (ActualHeight - s) / 2, s, s);
        }

        double Scale(Rect r) { return Math.Max(r.Width / img.PixelWidth, r.Height / img.PixelHeight) * zoom; }

        void Clamp()
        {
            Rect r = Region();
            double k = Scale(r);
            double mx = Math.Max(0, (img.PixelWidth * k - r.Width) / 2);
            double my = Math.Max(0, (img.PixelHeight * k - r.Height) / 2);
            off = new Vector(Math.Max(-mx, Math.Min(mx, off.X)), Math.Max(-my, Math.Min(my, off.Y)));
        }

        public void ZoomBy(double f)
        {
            zoom = Math.Max(1, Math.Min(8, zoom * f));
            Clamp(); InvalidateVisual();
        }

        public void Rotate()
        {
            TransformedBitmap tb = new TransformedBitmap(img, new RotateTransform(90));
            tb.Freeze();
            img = tb;
            zoom = 1; off = new Vector(0, 0);
            InvalidateVisual();
        }

        public void ResetView() { zoom = 1; off = new Vector(0, 0); InvalidateVisual(); }

        Rect ImageRect(Rect dest, Rect region)
        {
            double k = Scale(region);
            double f = dest.Width / region.Width;
            double w = img.PixelWidth * k, h = img.PixelHeight * k;
            double cx = region.X + region.Width / 2 + off.X, cy = region.Y + region.Height / 2 + off.Y;
            return new Rect(dest.X + (cx - w / 2 - region.X) * f, dest.Y + (cy - h / 2 - region.Y) * f, w * f, h * f);
        }

        Geometry Shape(Rect r)
        {
            if (circle) return new EllipseGeometry(r);
            double d = r.Width * 0.22 / 2;
            return new RectangleGeometry(r, d, d);
        }

        // a 256 px PNG of what is inside the frame
        public byte[] Render(int size)
        {
            DrawingVisual dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = dv.RenderOpen())
            {
                Rect dest = new Rect(0, 0, size, size);
                dc.PushClip(Shape(dest));
                dc.DrawImage(img, ImageRect(dest, Region()));
                dc.Pop();
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (MemoryStream ms = new MemoryStream()) { enc.Save(ms); return ms.ToArray(); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Theme.Bg1), null, new Rect(0, 0, ActualWidth, ActualHeight));
            Rect r = Region();
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            dc.DrawImage(img, ImageRect(r, r));
            Color dim = Theme.Bg1; dim.A = 190;
            GeometryGroup outer = new GeometryGroup();
            outer.FillRule = FillRule.EvenOdd;
            outer.Children.Add(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            outer.Children.Add(Shape(r));
            dc.DrawGeometry(new SolidColorBrush(dim), null, outer);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Theme.AccText), 2), Shape(r));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { drag = true; last = e.GetPosition(this); CaptureMouse(); base.OnMouseLeftButtonDown(e); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = false; ReleaseMouseCapture(); base.OnMouseLeftButtonUp(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag)
            {
                Point p = e.GetPosition(this);
                off += p - last;
                last = p;
                Clamp(); InvalidateVisual();
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            ZoomBy(Math.Pow(1.1, e.Delta / 120.0));
            base.OnMouseWheel(e);
        }
    }

    sealed partial class MainWindow
    {
        static BitmapSource LoadBitmap(string path)
        {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        // returns PNG bytes, or null when cancelled
        byte[] CropDialog(BitmapSource source, bool round, string title, string hint)
        {
            byte[] result = null;
            CropCanvas canvas = new CropCanvas(source, round);
            canvas.Height = 440;
            canvas.Margin = new Thickness(0, 10, 0, 14);
            Window win = null;
            Button zo = UI.Btn("Zoom out", "Ghost", delegate { canvas.ZoomBy(0.85); });
            Button zi = UI.Btn("Zoom in", "Ghost", delegate { canvas.ZoomBy(1.18); });
            Button ro = UI.Btn("Rotate", "Ghost", delegate { canvas.Rotate(); });
            Button rs = UI.Btn("Reset", "Ghost", delegate { canvas.ResetView(); });
            WrapPanel tools = new WrapPanel();
            foreach (Button b in new Button[] { zo, zi, ro, rs }) { b.Margin = new Thickness(0, 0, 8, 8); tools.Children.Add(b); }
            Button ok = UI.Btn("Save", "Pill", delegate { result = canvas.Render(256); win.Close(); });
            Button no = UI.Btn("Cancel", "Ghost", delegate { win.Close(); });
            no.Margin = new Thickness(8, 0, 0, 0);
            StackPanel btns = UI.Row(ok, no);
            btns.Margin = new Thickness(0, 8, 0, 0);
            win = NewWindow(title, UI.Col(UI.Wrap(hint, 13.5, "T2"), canvas, tools, btns), 560, 780);
            win.ShowDialog();
            return result;
        }

        string PickPictureFile()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Pictures (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp";
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        }

        // pick a picture, then crop and adjust it before it is saved (photo is round, app icon is a rounded square)
        void CropPicture(bool isPhoto)
        {
            string file = PickPictureFile();
            if (file == null) return;
            BitmapSource src;
            try { src = LoadBitmap(file); }
            catch (Exception) { stampText = "That picture could not be read."; stampBad = true; UpdateTop(); return; }
            byte[] png = CropDialog(src, isPhoto, isPhoto ? "Crop your photo" : "Crop the app icon",
                "Drag the picture to move it, scroll or use the buttons to zoom. Only what is inside the frame is kept.");
            if (png == null) return;
            try
            {
                if (isPhoto) { File.WriteAllBytes(Disk.At("photo.png"), png); photo = Decode(png); }
                else { File.WriteAllBytes(Disk.At("icon.png"), png); UpdateIcon(); }
            }
            catch (Exception ex) { Log.Error("saving cropped picture", ex); }
            AfterPictureChange();
        }

        void AfterPictureChange()
        {
            if (brandHost != null) brandHost.Content = LogoMark(44);
            UpdateTop();
            if (picHost != null && view == "settings") picHost.Refresh();
            else if (view == "dashboard") ShowView(false);
        }

        void ChooseImage(string fileName, bool isPhoto)
        {
            string file = PickPictureFile();
            if (file == null) return;
            try
            {
                BitmapSource src = LoadBitmap(file);
                PngBitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(src));
                byte[] png;
                using (MemoryStream ms = new MemoryStream()) { enc.Save(ms); png = ms.ToArray(); }
                File.WriteAllBytes(Disk.At(fileName), png);
                if (isPhoto) photo = Decode(png); else logo = Decode(png);
                AfterPictureChange();
            }
            catch (Exception) { stampText = "That picture could not be read."; stampBad = true; UpdateTop(); }
        }

        void RemoveCustom(string fileName, bool isPhoto)
        {
            try { if (File.Exists(Disk.At(fileName))) File.Delete(Disk.At(fileName)); } catch (Exception) { }
            if (isPhoto) photo = null; else logo = null;
            LoadLocalImages();
            UpdateIcon();
            if (api != null && current != null) LoadAssets(api, current, true);
            AfterPictureChange();
        }
    }
}
