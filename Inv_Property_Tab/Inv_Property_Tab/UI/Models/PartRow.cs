using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Inventor;

namespace Inv_Property_Tab.UI.Models
{
    public class PartRow : INotifyPropertyChanged
    {
        public Document Document { get; }

        public string Name => Document.DisplayName;
        public string FullFileName => Document.FullFileName;

        public string Location { get; }
        public string Size { get; }
        public string Created { get; }
        public string Saved { get; }
        public string DevelopedBy { get; }
        public string Project { get; }
        public string Mass { get; }
        public string Vendor { get; }
        public string Material { get; }

        public string Supplier { get; }

        public string[] OrientationOptions { get; } = new[] { "XZ", "XY", "YZ" };

        private string _orientation = "XZ";
        public string Orientation
        {
            get => _orientation;
            set
            {
                _orientation = value;
                OnPropertyChanged();
            }
        }

        public ImageSource? Thumbnail { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public PartRow(Document doc)
        {
            Document = doc;

            Location = TryGetLocation(doc) ?? string.Empty;
            Size = TryGetSize(doc) ?? string.Empty;
            Created = TryGetCreated(doc) ?? string.Empty;
            Saved = TryGetSaved(doc) ?? string.Empty;
            DevelopedBy = TryGetIProperty(doc, new[] { "Designer", "Разработал" }) ?? string.Empty;
            Project = TryGetIProperty(doc, new[] { "Project", "Проект" }) ?? string.Empty;
            Mass = TryGetMass(doc) ?? string.Empty;
            Material = TryGetIProperty(doc, new[] { "Material", "Материал" }) ?? string.Empty;

            Supplier = TryGetSupplier(doc) ?? string.Empty;
            Vendor = Supplier;

            try
            {
                // В Inventor API Thumbnail обычно возвращается как stdole.IPictureDisp,
                // но чтобы не тянуть stdole как reference, читаем свойство через reflection.
                var pic = doc.GetType().InvokeMember(
                    "Thumbnail",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    doc,
                    null);

                if (pic != null)
                {
                    var converted = AxHostConverter.PictureDispToImage(pic);

                    if (converted is Image img)
                    {
                        Thumbnail = ToBitmapSource(img);
                        img.Dispose();
                    }
                    else if (converted is Icon ico)
                    {
                        using (var bmp = ico.ToBitmap())
                            Thumbnail = ToBitmapSource(bmp);
                    }
                }
            }
            catch
            {
                Thumbnail = CreateFallbackThumbnail();
            }

            if (Thumbnail == null)
                Thumbnail = CreateFallbackThumbnail();
        }

        private static string? TryGetSupplier(Document doc)
        {
            try
            {
                // Обычно это iProperties: Design Tracking Properties -> Vendor.
                // В русской локализации может называться "Поставщик".
                var propSets = doc.PropertySets;

                // 1) Быстрые попытки по известным наборам/именам
                var candidates = new (string setName, string propName)[]
                {
                    ("Design Tracking Properties", "Vendor"),
                    ("Design Tracking Properties", "Supplier"),
                    ("Design Tracking Properties", "Поставщик"),
                    ("Свойства отслеживания проекта", "Поставщик"),
                    ("Свойства отслеживания проекта", "Vendor"),
                };

                foreach (var c in candidates)
                {
                    try
                    {
                        var ps = propSets[c.setName];
                        var p = ps[c.propName];
                        var v = p?.Value;
                        var s = v?.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s;
                    }
                    catch
                    {
                    }
                }

                // 2) Общий поиск по всем PropertySet/Property, чтобы пережить локализацию
                foreach (PropertySet ps in propSets)
                {
                    foreach (Inventor.Property p in ps)
                    {
                        var name = p?.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (!name.Equals("Vendor", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("Supplier", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("Поставщик", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var s = p.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? TryGetLocation(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p))
                    return null;
                return System.IO.Path.GetDirectoryName(p);
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetCreated(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return null;
                return System.IO.File.GetCreationTime(p).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetSaved(Document doc)
        {
            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return null;
                return System.IO.File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetSize(Document doc)
        {
            try
            {
                if (doc is PartDocument pd)
                {
                    var rb = pd.ComponentDefinition.RangeBox;
                    var uom = pd.UnitsOfMeasure;
                    var dx = Math.Abs(rb.MaxPoint.X - rb.MinPoint.X);
                    var dy = Math.Abs(rb.MaxPoint.Y - rb.MinPoint.Y);
                    var dz = Math.Abs(rb.MaxPoint.Z - rb.MinPoint.Z);
                    var x = uom.ConvertUnits(dx, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);
                    var y = uom.ConvertUnits(dy, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);
                    var z = uom.ConvertUnits(dz, UnitsTypeEnum.kDatabaseLengthUnits, UnitsTypeEnum.kMillimeterLengthUnits);
                    return $"{x:0.#} x {y:0.#} x {z:0.#} mm";
                }
            }
            catch
            {
            }

            try
            {
                var p = doc.FullFileName;
                if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p))
                    return null;
                var fi = new System.IO.FileInfo(p);
                return $"{fi.Length / 1024.0:0.#} KB";
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetMass(Document doc)
        {
            try
            {
                if (doc is PartDocument pd)
                {
                    var mp = pd.ComponentDefinition.MassProperties;
                    var mass = mp.Mass;
                    return $"{mass:0.###} kg";
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? TryGetIProperty(Document doc, string[] propertyNames)
        {
            try
            {
                var propSets = doc.PropertySets;
                foreach (PropertySet ps in propSets)
                {
                    foreach (Inventor.Property p in ps)
                    {
                        var name = p?.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (!propertyNames.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var s = p.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static ImageSource ToBitmapSource(Image img)
        {
            using (var bmp = new Bitmap(img))
            {
                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(64, 64));

                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        private static ImageSource CreateFallbackThumbnail()
        {
            using (var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.WhiteSmoke);
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.Silver))
                    g.DrawRectangle(pen, 1, 1, 62, 62);

                using (var brush = new SolidBrush(System.Drawing.Color.DimGray))
                using (var font = new Font("Segoe UI", 8f))
                {
                    var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("No\nPreview", font, brush, new RectangleF(0, 0, 64, 64), fmt);
                }

                return ToBitmapSource(bmp);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private class AxHostConverter : System.Windows.Forms.AxHost
        {
            private AxHostConverter() : base(string.Empty) { }

            public static object PictureDispToImage(object pictureDisp)
            {
                return GetPictureFromIPicture(pictureDisp);
            }
        }
    }
}
