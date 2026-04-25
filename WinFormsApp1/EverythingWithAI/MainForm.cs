namespace EverythingWithAI;

public class MainForm : Form
{
    // ── 控制項 ──────────────────────────────────────────
    private ComboBox  _cmbLang    = null!;

    private GroupBox  _grpApi     = null!;
    private Label     _lblKey     = null!;
    private TextBox   txtApiKey   = null!;
    private CheckBox  chkShowKey  = null!;
    private Button    btnSaveKey  = null!;

    private GroupBox  _grpSearch  = null!;
    private Label     _lblQ       = null!;
    private TextBox   txtQuery    = null!;
    private Button    btnSearch   = null!;
    private Label     _lblSyn     = null!;
    private TextBox   txtSyntax   = null!;

    private Label         lblResultCount = null!;
    private FileCardPanel _filePanel     = null!;
    private Label         lblStatus      = null!;

    private AppSettings _settings;
    private bool _langBusy;   // 防止 ComboBox TextChanged 遞迴

    // ── 建構子 ──────────────────────────────────────────
    public MainForm()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        txtApiKey.Text = _settings.ApiKey;
    }

    // ── UI 建立 ─────────────────────────────────────────
    private void InitializeComponent()
    {
        this.Text          = Strings.FormTitle;
        this.Size          = new Size(860, 652);
        this.MinimumSize   = new Size(660, 530);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font          = new Font(Strings.UiFont, 9.5f);

        // ── 語系選擇列（頂端 32px 列） ───────────────────
        var lblGlobe = new Label
        {
            Text = "🌐",
            Left = 12, Top = 6, Width = 24, Height = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 11f)
        };

        _cmbLang = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Left = 40, Top = 5,
            Width = 210, Height = 24,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        foreach (var (_, display) in Strings.Languages)
            _cmbLang.Items.Add(display);

        // 選到目前語系
        var curDisplay = Strings.Languages.FirstOrDefault(x => x.Code == Strings.Lang).Display;
        _cmbLang.Text = curDisplay ?? Strings.Languages[0].Display;

        _cmbLang.TextChanged        += CmbLang_TextChanged;
        _cmbLang.SelectedIndexChanged += CmbLang_SelectedIndexChanged;

        // ── API Key GroupBox ────────────────────────────
        _grpApi = new GroupBox
        {
            Text = Strings.GroupApi,
            Left = 12, Top = 42,
            Width = this.ClientSize.Width - 24, Height = 64,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblKey = new Label
        {
            Text = Strings.LblApiKey,
            Left = 8, Top = 24, Width = 70, Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };

        txtApiKey = new TextBox
        {
            Left = 82, Top = 22, Width = _grpApi.Width - 270, Height = 24,
            UseSystemPasswordChar = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        chkShowKey = new CheckBox
        {
            Text = Strings.ChkShow,
            Left = txtApiKey.Right + 6, Top = 23,
            Width = 52, Height = 22,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        chkShowKey.CheckedChanged += (_, _) =>
            txtApiKey.UseSystemPasswordChar = !chkShowKey.Checked;

        btnSaveKey = new Button
        {
            Text = Strings.BtnSave,
            Left = chkShowKey.Right + 6, Top = 21,
            Width = 60, Height = 26,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnSaveKey.Click += BtnSaveKey_Click;

        _grpApi.Controls.AddRange([_lblKey, txtApiKey, chkShowKey, btnSaveKey]);

        // ── 搜尋 GroupBox ───────────────────────────────
        _grpSearch = new GroupBox
        {
            Text = Strings.GroupSearch,
            Left = 12, Top = 114,
            Width = this.ClientSize.Width - 24, Height = 96,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblQ = new Label
        {
            Text = Strings.LblNatural,
            Left = 8, Top = 24, Width = 74, Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };

        txtQuery = new TextBox
        {
            Left = 86, Top = 22,
            Width = _grpSearch.Width - 200, Height = 26,
            PlaceholderText = Strings.TxtPlaceholder,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        txtQuery.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnSearch_Click(null, EventArgs.Empty); }
        };

        btnSearch = new Button
        {
            Text = Strings.BtnSearch,
            Left = txtQuery.Right + 8, Top = 20,
            Width = 80, Height = 30,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSearch.FlatAppearance.BorderSize = 0;
        btnSearch.Click += BtnSearch_Click;

        _lblSyn = new Label
        {
            Text = Strings.LblSyntax,
            Left = 8, Top = 58, Width = 100, Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };

        txtSyntax = new TextBox
        {
            Left = 112, Top = 56,
            Width = _grpSearch.Width - 124, Height = 24,
            ReadOnly = true,
            BackColor = Color.FromArgb(245, 245, 245),
            ForeColor = Color.DarkGreen,
            Font = new Font("Consolas", 10f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _grpSearch.Controls.AddRange([_lblQ, txtQuery, btnSearch, _lblSyn, txtSyntax]);

        // ── 結果區 ──────────────────────────────────────
        lblResultCount = new Label
        {
            Text = Strings.LblResultsEmpty,
            Left = 12, Top = 220,
            Width = 300, Height = 20,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };

        _filePanel = new FileCardPanel
        {
            Left = 12, Top = 242,
            Width = this.ClientSize.Width - 24,
            Height = this.ClientSize.Height - 290,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        lblStatus = new Label
        {
            Text = Strings.StatusReady,
            Left = 12,
            Top = this.ClientSize.Height - 42,
            Width = this.ClientSize.Width - 24, Height = 22,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.Gray
        };

        this.Controls.AddRange([lblGlobe, _cmbLang, _grpApi, _grpSearch,
                                 lblResultCount, _filePanel, lblStatus]);

        // 視窗縮放時修正 GroupBox 子控項的寬度
        this.Resize += (_, _) =>
        {
            txtApiKey.Width   = _grpApi.Width - 270;
            chkShowKey.Left   = txtApiKey.Right + 6;
            btnSaveKey.Left   = chkShowKey.Right + 6;
            txtQuery.Width    = _grpSearch.Width - 200;
            btnSearch.Left    = txtQuery.Right + 8;
            txtSyntax.Width   = _grpSearch.Width - 124;
        };
    }

    // ── 語系 ComboBox：即時過濾 ─────────────────────────
    private void CmbLang_TextChanged(object? sender, EventArgs e)
    {
        if (_langBusy) return;
        _langBusy = true;

        string typed   = _cmbLang.Text;
        int    cursor  = _cmbLang.SelectionStart;
        string filter  = typed.ToLowerInvariant();

        _cmbLang.Items.Clear();
        foreach (var (code, display) in Strings.Languages)
        {
            if (display.ToLowerInvariant().Contains(filter) ||
                code.ToLowerInvariant().Contains(filter))
                _cmbLang.Items.Add(display);
        }

        _cmbLang.Text            = typed;
        _cmbLang.SelectionStart  = cursor;
        _cmbLang.SelectionLength = 0;

        if (_cmbLang.Items.Count > 0 && typed.Length > 0)
            BeginInvoke(() => { if (!_cmbLang.DroppedDown) _cmbLang.DroppedDown = true; });

        _langBusy = false;
    }

    // ── 語系 ComboBox：確認選擇 → 套用語系 ─────────────
    private void CmbLang_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_langBusy) return;
        if (_cmbLang.SelectedIndex < 0) return;

        var selected = _cmbLang.SelectedItem?.ToString() ?? "";
        var match    = Strings.Languages.FirstOrDefault(x => x.Display == selected);
        if (match.Code is null) return;

        Strings.Lang = match.Code;
        ApplyLanguage();
    }

    // ── 語系套用（重新設定所有控制項文字與字型） ────────
    private void ApplyLanguage()
    {
        // 字型
        this.Font = new Font(Strings.UiFont, 9.5f);

        // 主視窗
        this.Text = Strings.FormTitle;

        // API 群組
        _grpApi.Text      = Strings.GroupApi;
        _lblKey.Text      = Strings.LblApiKey;
        chkShowKey.Text   = Strings.ChkShow;
        btnSaveKey.Text   = Strings.BtnSave;

        // 搜尋群組
        _grpSearch.Text      = Strings.GroupSearch;
        _lblQ.Text           = Strings.LblNatural;
        txtQuery.PlaceholderText = Strings.TxtPlaceholder;
        btnSearch.Text       = Strings.BtnSearch;
        _lblSyn.Text         = Strings.LblSyntax;

        // 結果 / 狀態
        if (lblResultCount.Text != Strings.LblResultsEmpty)
            // 若目前顯示的是「共 N 筆」，保持數字但換語言
            lblResultCount.Text = Strings.LblResultsEmpty;
        lblStatus.Text  = Strings.StatusReady;

        // ComboBox 顯示目前語系名稱（不觸發 TextChanged 遞迴）
        _langBusy = true;
        _cmbLang.Items.Clear();
        foreach (var (_, display) in Strings.Languages)
            _cmbLang.Items.Add(display);
        _cmbLang.Text = Strings.Languages.FirstOrDefault(x => x.Code == Strings.Lang).Display ?? "";
        _langBusy = false;

        // FileCardPanel
        _filePanel.ApplyLanguage();
    }

    // ── 事件：儲存 API Key ──────────────────────────────
    private void BtnSaveKey_Click(object? sender, EventArgs e)
    {
        _settings.ApiKey = txtApiKey.Text.Trim();
        _settings.Save();
        SetStatus(Strings.StatusKeySaved);
    }

    // ── 事件：搜尋 ─────────────────────────────────────
    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        var naturalText = txtQuery.Text.Trim();
        if (string.IsNullOrEmpty(naturalText))
        {
            MessageBox.Show(Strings.MsgNeedQuery, Strings.MsgTitleInfo,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var apiKey = txtApiKey.Text.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show(Strings.MsgNeedKey, Strings.MsgTitleWarn,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnSearch.Enabled = false;
        _filePanel.SetItems([]);
        txtSyntax.Text = string.Empty;
        lblResultCount.Text = Strings.LblResultsEmpty;
        SetStatus(Strings.StatusAsking);

        try
        {
            var claude = new ClaudeService(apiKey);
            var query  = await claude.ConvertToEverythingQuery(naturalText);
            txtSyntax.Text = query;
            SetStatus(Strings.StatusSearching(query));

            var results = await Task.Run(() => EverythingSearch.Search(query, uint.MaxValue));

            _filePanel.SetItems(results);
            lblResultCount.Text = Strings.LblResultsCount(results.Count);
            SetStatus(Strings.StatusDone(results.Count));
        }
        catch (Exception ex)
        {
            SetStatus(Strings.StatusError(ex.Message), isError: true);
            MessageBox.Show(ex.Message, Strings.MsgTitleError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSearch.Enabled = true;
        }
    }

    // ── 狀態列更新 ──────────────────────────────────────
    private void SetStatus(string msg, bool isError = false)
    {
        lblStatus.Text     = msg;
        lblStatus.ForeColor = isError ? Color.Red : Color.Gray;
    }
}
