using System.Diagnostics;

namespace EverythingWithAI;

/// <summary>
/// 虛擬滾動的檔案卡片清單，支援數萬筆結果而不卡頓。
/// 單擊開啟檔案，右鍵選單可在檔案總管中顯示。
/// </summary>
public sealed class FileCardPanel : Panel
{
    // ── Layout constants ────────────────────────────────
    private const int ItemH  = 70;   // card height (px)
    private const int Gap    = 2;    // gap between cards
    private const int IconW  = 32;   // icon size
    private const int PadL   = 12;   // left padding
    private const int PadR   = 10;   // right padding

    // ── Data ────────────────────────────────────────────
    private List<string> _paths = [];
    private readonly Dictionary<string, Bitmap?> _iconCache  = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (string size, string date)> _metaCache = [];

    // ── Controls ────────────────────────────────────────
    private readonly VScrollBar      _vsb = new();
    private readonly ContextMenuStrip _ctx = new();

    private int     _hover   = -1;
    private string? _ctxPath;

    // 存放選單項目以便 ApplyLanguage 更新文字
    private readonly ToolStripMenuItem _miOpen;
    private readonly ToolStripMenuItem _miExplore;

    // ── Pre-allocated GDI objects (disposed with panel) ─
    private Font _fName  = new(Strings.UiFont, 10f, FontStyle.Bold);
    private Font _fPath  = new("Consolas", 8f);
    private Font _fMeta  = new(Strings.UiFont, 7.5f);
    private Font _fBadge = new("Arial", 6f, FontStyle.Bold);
    private Font _fEmpty = new(Strings.UiFont, 11f);

    private readonly SolidBrush _brName   = new(Color.FromArgb(22, 22, 22));
    private readonly SolidBrush _brPath   = new(Color.FromArgb(105, 105, 105));
    private readonly SolidBrush _brMeta   = new(Color.FromArgb(130, 130, 130));
    private readonly SolidBrush _brWhite  = new(Color.White);
    private readonly SolidBrush _brBgNorm = new(Color.White);
    private readonly SolidBrush _brBgHov  = new(Color.FromArgb(232, 244, 255));
    private readonly SolidBrush _brEmpty  = new(Color.FromArgb(160, 160, 160));

    private readonly Pen _penNorm = new(Color.FromArgb(228, 228, 231));
    private readonly Pen _penHov  = new(Color.FromArgb(0, 120, 212), 1.5f);

    private readonly StringFormat _sfLeft  = new() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
    private readonly StringFormat _sfPth   = new() { Trimming = StringTrimming.EllipsisPath,      FormatFlags = StringFormatFlags.NoWrap };
    private readonly StringFormat _sfRight = new() { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
    private readonly StringFormat _sfCtr   = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

    // ── Constructor ─────────────────────────────────────
    public FileCardPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(241, 242, 244);

        // Scroll bar
        _vsb.Dock = DockStyle.Right;
        _vsb.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_vsb);

        // Context menu
        _miOpen    = new ToolStripMenuItem(Strings.CtxOpen);
        _miExplore = new ToolStripMenuItem(Strings.CtxExplore);
        _miOpen.Click    += (_, _) => DoOpen(_ctxPath);
        _miExplore.Click += (_, _) => DoExplore(_ctxPath);
        _ctx.Items.AddRange([_miOpen, new ToolStripSeparator(), _miExplore]);
    }

    // ── Public API ──────────────────────────────────────
    public void SetItems(IReadOnlyList<string> paths)
    {
        _paths = [.. paths];
        _metaCache.Clear();
        _hover = -1;
        _vsb.Value = 0;
        RefreshVsb();
        Invalidate();
    }

    // ── Geometry helpers ─────────────────────────────────
    private int StepH  => ItemH + Gap;
    private int Offset => _vsb.Visible ? _vsb.Value : 0;
    private int ListW  => ClientSize.Width - (_vsb.Visible ? _vsb.Width : 0);

    private void RefreshVsb()
    {
        int total = _paths.Count * StepH;
        int view  = Math.Max(1, ClientSize.Height);
        _vsb.LargeChange = view;
        _vsb.SmallChange = StepH;
        if (total <= view) { _vsb.Visible = false; return; }
        _vsb.Visible = true;
        _vsb.Maximum = total - view + _vsb.LargeChange - 1;
        int cap = Math.Max(0, total - view);
        if (_vsb.Value > cap) _vsb.Value = cap;
    }

    private int HitIndex(int mouseY)
    {
        int i = (mouseY + Offset) / StepH;
        return i >= 0 && i < _paths.Count ? i : -1;
    }

    // ── Mouse ─────────────────────────────────────────────
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_vsb.Visible) return;
        int cap = Math.Max(0, _vsb.Maximum - _vsb.LargeChange + 1);
        _vsb.Value = Math.Clamp(_vsb.Value - e.Delta / 120 * StepH * 3, 0, cap);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitIndex(e.Y);
        if (h == _hover) return;
        _hover  = h;
        Cursor  = h >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int i = HitIndex(e.Y);
        if (i < 0) return;

        if (e.Button == MouseButtons.Left)
            DoOpen(_paths[i]);
        else if (e.Button == MouseButtons.Right)
        {
            _ctxPath = _paths[i];
            _ctx.Show(this, e.Location);
        }
    }

    // ── Paint ─────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        if (_paths.Count == 0)
        {
            g.DrawString(Strings.NoResults, _fEmpty, _brEmpty, new PointF(PadL, 20));
            return;
        }

        // Only paint cards within the visible clip rectangle
        int first = Math.Max(0, (Offset + e.ClipRectangle.Top) / StepH);
        int last  = Math.Min(_paths.Count - 1,
                             (Offset + e.ClipRectangle.Bottom + StepH - 1) / StepH);

        for (int i = first; i <= last; i++)
        {
            int y = i * StepH - Offset;
            DrawCard(g, i, new Rectangle(0, y, ListW, ItemH));
        }
    }

    private void DrawCard(Graphics g, int idx, Rectangle r)
    {
        string path = _paths[idx];
        bool   hov  = idx == _hover;

        // ── Background + border
        g.FillRectangle(hov ? _brBgHov : _brBgNorm, r);
        var pen = hov ? _penHov : _penNorm;
        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);

        // Accent bar on left edge when hovered
        if (hov)
        {
            using var accent = new SolidBrush(Color.FromArgb(0, 120, 212));
            g.FillRectangle(accent, r.X, r.Y + 2, 3, r.Height - 4);
        }

        // ── Icon (32×32, centred vertically)
        int iconX = r.Left + PadL + (hov ? 3 : 0);
        int iconY = r.Top + (r.Height - IconW) / 2;
        var bmp = GetIcon(path);
        if (bmp != null)
            g.DrawImage(bmp, new Rectangle(iconX, iconY, IconW, IconW));

        // ── Extension badge (bottom-right corner of icon)
        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (ext.Length is >= 1 and <= 6)
        {
            string badge = ext.Length > 4 ? ext[..4] : ext;
            var    badgeR = new Rectangle(iconX + IconW - 20, iconY + IconW - 12, 20, 12);
            using var bb = new SolidBrush(BadgeColor(ext));
            g.FillRectangle(bb, badgeR);
            g.DrawString(badge, _fBadge, _brWhite, badgeR, _sfCtr);
        }

        // ── Text column
        int tx = iconX + IconW + 10;
        int tw = r.Right - tx - PadR;

        // File name (bold, large)
        g.DrawString(Path.GetFileName(path), _fName, _brName,
            new RectangleF(tx, r.Top + 7, tw, 21), _sfLeft);

        // Directory path
        g.DrawString(Path.GetDirectoryName(path) ?? string.Empty, _fPath, _brPath,
            new RectangleF(tx, r.Top + 30, tw, 17), _sfPth);

        // Size + date (right-aligned, bottom row)
        if (!_metaCache.TryGetValue(idx, out var meta))
            _metaCache[idx] = meta = FetchMeta(path);
        if (meta.size.Length > 0)
            g.DrawString($"{meta.size}   {meta.date}", _fMeta, _brMeta,
                new RectangleF(tx, r.Top + 49, tw, 16), _sfRight);
    }

    // ── Icon cache (keyed by extension or "<dir>") ───────
    private Bitmap? GetIcon(string path)
    {
        bool isDir = Directory.Exists(path);
        string key = isDir ? "<dir>"
            : Path.GetExtension(path) is { Length: > 0 } e
                ? e.ToLowerInvariant()
                : "<file>";

        if (_iconCache.TryGetValue(key, out var cached)) return cached;

        Bitmap? bmp = null;
        try
        {
            if (isDir || File.Exists(path))
            {
                using var ico = Icon.ExtractAssociatedIcon(path);
                bmp = ico?.ToBitmap();
            }
        }
        catch { }

        _iconCache[key] = bmp;
        return bmp;
    }

    // ── Metadata (lazy, cached) ──────────────────────────
    private static (string size, string date) FetchMeta(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                return (FmtSize(fi.Length), fi.LastWriteTime.ToString("yyyy/MM/dd HH:mm"));
            }
            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                return (Strings.MetaFolder, di.LastWriteTime.ToString("yyyy/MM/dd HH:mm"));
            }
        }
        catch { }
        return (string.Empty, string.Empty);
    }

    private static string FmtSize(long b) => b switch
    {
        < 1_024           => $"{b} B",
        < 1_048_576       => $"{b / 1_024.0:F1} KB",
        < 1_073_741_824   => $"{b / 1_048_576.0:F1} MB",
        _                 => $"{b / 1_073_741_824.0:F2} GB"
    };

    // ── Extension → badge colour ─────────────────────────
    private static Color BadgeColor(string e) => e switch
    {
        "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" or "TS"
            => Color.FromArgb(192, 57, 43),
        "JPG" or "JPEG" or "PNG" or "GIF" or "BMP" or "WEBP" or "SVG" or "TIFF" or "HEIC"
            => Color.FromArgb(41, 128, 185),
        "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "M4A" or "OPUS"
            => Color.FromArgb(142, 68, 173),
        "PDF"
            => Color.FromArgb(200, 40, 40),
        "DOC" or "DOCX"
            => Color.FromArgb(28, 90, 160),
        "XLS" or "XLSX" or "CSV"
            => Color.FromArgb(30, 130, 70),
        "PPT" or "PPTX"
            => Color.FromArgb(185, 70, 20),
        "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" or "XZ"
            => Color.FromArgb(100, 75, 55),
        "EXE" or "MSI" or "DLL"
            => Color.FromArgb(50, 70, 90),
        "TXT" or "MD" or "LOG"
            => Color.FromArgb(85, 105, 115),
        "CS" or "PY" or "JS" or "TS" or "GO" or "RS" or "CPP" or "C" or "JAVA" or "RB" or "PHP"
            => Color.FromArgb(0, 148, 133),
        "HTML" or "CSS" or "XML" or "JSON" or "YAML" or "TOML" or "INI"
            => Color.FromArgb(210, 82, 0),
        _   => Color.FromArgb(108, 117, 125)
    };

    // ── Actions ──────────────────────────────────────────
    private static void DoOpen(string? p)
    {
        if (string.IsNullOrEmpty(p)) return;
        try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); }
        catch { }
    }

    private static void DoExplore(string? p)
    {
        if (string.IsNullOrEmpty(p)) return;
        try { Process.Start("explorer.exe", $"/select,\"{p}\""); }
        catch { }
    }

    // ── 語系更新 ─────────────────────────────────────────
    public void ApplyLanguage()
    {
        _miOpen.Text    = Strings.CtxOpen;
        _miExplore.Text = Strings.CtxExplore;

        // 若字型家族改變，重建字型
        string uiFont = Strings.UiFont;
        if (_fName.FontFamily.Name != uiFont)
        {
            _fName.Dispose();  _fName  = new Font(uiFont, 10f, FontStyle.Bold);
            _fMeta.Dispose();  _fMeta  = new Font(uiFont, 7.5f);
            _fEmpty.Dispose(); _fEmpty = new Font(uiFont, 11f);
        }

        Invalidate();
    }

    // ── Resize ────────────────────────────────────────────
    protected override void OnResize(EventArgs e) { base.OnResize(e); RefreshVsb(); Invalidate(); }

    // ── Dispose ──────────────────────────────────────────
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fName.Dispose(); _fPath.Dispose(); _fMeta.Dispose();
            _fBadge.Dispose(); _fEmpty.Dispose();
            _brName.Dispose(); _brPath.Dispose(); _brMeta.Dispose();
            _brWhite.Dispose(); _brBgNorm.Dispose(); _brBgHov.Dispose(); _brEmpty.Dispose();
            _penNorm.Dispose(); _penHov.Dispose();
            _sfLeft.Dispose(); _sfPth.Dispose(); _sfRight.Dispose(); _sfCtr.Dispose();
            foreach (var b in _iconCache.Values) b?.Dispose();
            _ctx.Dispose();
        }
        base.Dispose(disposing);
    }
}
