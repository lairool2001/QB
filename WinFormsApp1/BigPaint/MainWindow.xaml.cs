using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BigPaint
{
    public partial class MainWindow : Window
    {
        private static readonly string SaveFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "BigPaint");

        public static readonly RoutedCommand OpenFolderCommand = new();

        private readonly Stack<StrokeCollectionChangedEventArgs> _undo = new();
        private readonly Stack<StrokeCollectionChangedEventArgs> _redo = new();
        private bool _applyingHistory;
        private Color _color = Colors.Black;
        private BitmapSource? _image; // 底圖（開啟檔案或貼上）

        public MainWindow()
        {
            InitializeComponent();

            Palette.ItemsSource = new[]
            {
                Colors.Black, Colors.Gray, Colors.White, Colors.Red, Colors.Orange, Colors.Gold,
                Colors.LimeGreen, Colors.Green, Colors.DeepSkyBlue, Colors.Blue, Colors.Purple, Colors.HotPink,
                Colors.SaddleBrown,
            };

            Canvas.Strokes.StrokesChanged += Strokes_StrokesChanged;
            UpdatePen();
        }

        private void Strokes_StrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
        {
            if (_applyingHistory) return;
            _undo.Push(e);
            _redo.Clear();
        }

        private void UpdatePen()
        {
            if (Canvas == null || SizeText == null) return;

            double size = SizeSlider.Value;
            SizeText.Text = ((int)size).ToString();
            CurrentColor.Background = new SolidColorBrush(_color);

            bool highlighter = HighlighterTool.IsChecked == true;
            Canvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _color,
                Width = highlighter ? size * 3 : size,
                Height = highlighter ? size * 3 : size,
                IsHighlighter = highlighter,
                StylusTip = highlighter ? StylusTip.Rectangle : StylusTip.Ellipse,
                FitToCurve = true,
            };
            Canvas.EraserShape = new EllipseStylusShape(size * 2, size * 2);

            if (PenTool.IsChecked == true || highlighter) Canvas.EditingMode = InkCanvasEditingMode.Ink;
            else if (EraserTool.IsChecked == true) Canvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            else if (StrokeEraserTool.IsChecked == true) Canvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            else if (SelectTool.IsChecked == true) Canvas.EditingMode = InkCanvasEditingMode.Select;

            // EraserShape only takes effect after re-entering erase mode
            if (Canvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                Canvas.EditingMode = InkCanvasEditingMode.None;
                Canvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
        }

        private void Tool_Checked(object sender, RoutedEventArgs e) => UpdatePen();

        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePen();

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            _color = (Color)((Button)sender).Tag;
            if (PenTool.IsChecked != true && HighlighterTool.IsChecked != true) PenTool.IsChecked = true;
            UpdatePen();
        }

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_undo.Count == 0) return;
            var change = _undo.Pop();
            _applyingHistory = true;
            Canvas.Strokes.Remove(change.Added);
            Canvas.Strokes.Add(change.Removed);
            _applyingHistory = false;
            _redo.Push(change);
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_redo.Count == 0) return;
            var change = _redo.Pop();
            _applyingHistory = true;
            Canvas.Strokes.Remove(change.Removed);
            Canvas.Strokes.Add(change.Added);
            _applyingHistory = false;
            _undo.Push(change);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (Canvas.Strokes.Count == 0) return;
            // Clear() fires a single StrokesChanged, so it can be undone in one step
            Canvas.Strokes.Clear();
            StatusText.Text = "已清除（Ctrl+Z 可復原）";
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Directory.CreateDirectory(SaveFolder);
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = SaveFolder,
                Filter = "圖片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有檔案|*.*",
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                SetImage(LoadFile(dlg.FileName));
                ResetStrokes();
                Title = $"小畫家 - {Path.GetFileName(dlg.FileName)}";
                StatusText.Text = $"已開啟：{dlg.FileName}（{_image!.PixelWidth}×{_image.PixelHeight}）";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"開啟失敗：{ex.Message}";
            }
        }

        // InkCanvas has its own Ctrl+V (ink clipboard), so intercept before it does
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Paste();
                e.Handled = true;
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e) => Paste();

        // 貼上剪貼簿圖片當作底圖；已畫的筆劃保留
        private void Paste()
        {
            try
            {
                BitmapSource? img = null;
                if (Clipboard.ContainsFileDropList())
                {
                    // files copied in Explorer
                    foreach (string? file in Clipboard.GetFileDropList())
                    {
                        if (file == null) continue;
                        try { img = LoadFile(file); break; } catch { }
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    img = Clipboard.GetImage();
                }

                if (img == null)
                {
                    StatusText.Text = "剪貼簿裡沒有圖片";
                    return;
                }

                if (img.CanFreeze) img.Freeze();
                SetImage(img);
                StatusText.Text = $"已貼上（{img.PixelWidth}×{img.PixelHeight}）";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"貼上失敗：{ex.Message}";
            }
        }

        private static BitmapSource LoadFile(string path)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad; // don't lock the file
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }

        // one image pixel = one DIP, matching the 96-dpi export in Save_Executed
        private void SetImage(BitmapSource img)
        {
            _image = img;
            BackImage.Source = img;
            BackImage.Width = img.PixelWidth;
            BackImage.Height = img.PixelHeight;
            Paper.Width = img.PixelWidth;
            Paper.Height = img.PixelHeight;
        }

        private void ResetStrokes()
        {
            _applyingHistory = true;
            Canvas.Strokes.Clear();
            _applyingHistory = false;
            _undo.Clear();
            _redo.Clear();
        }

        // paper always fills at least the visible area; a larger image scrolls
        private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Paper.MinWidth = Scroller.ViewportWidth;
            Paper.MinHeight = Scroller.ViewportHeight;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SaveFolder);
                string path = NextFileName();

                int w = (int)Paper.ActualWidth, h = (int)Paper.ActualHeight;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
                    if (_image != null)
                        dc.DrawImage(_image, new Rect(0, 0, _image.PixelWidth, _image.PixelHeight));
                    Canvas.Strokes.Draw(dc);
                }
                var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = new FileStream(path, FileMode.CreateNew))
                    encoder.Save(fs);

                StatusText.Text = $"已存檔：{path}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"存檔失敗：{ex.Message}";
            }
        }

        // yyyyMMdd-HHmmss-001.png，同一秒內多次存檔流水號遞增
        private static string NextFileName()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            for (int i = 1; ; i++)
            {
                string path = Path.Combine(SaveFolder, $"{stamp}-{i:D3}.png");
                if (!File.Exists(path)) return path;
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(SaveFolder);
            Process.Start(new ProcessStartInfo(SaveFolder) { UseShellExecute = true });
        }
    }
}
