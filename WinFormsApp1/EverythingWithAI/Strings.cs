using System.Globalization;

namespace EverythingWithAI;

/// <summary>
/// 多語系資源。啟動時偵測系統語系一次，之後不再變動。
/// 支援：zh-TW / zh-CN / en / ja / ko / fr / de / uk / es / pt / eo
/// </summary>
public static class Strings
{
    // ── 語系偵測 ────────────────────────────────────────
    public static string Lang { get; set; } = Detect();

    // 語系清單（供 ComboBox 使用）
    public static readonly IReadOnlyList<(string Code, string Display)> Languages =
    [
        ("zh-TW", "繁體中文 (zh-TW)"),
        ("zh-CN", "简体中文 (zh-CN)"),
        ("en",    "English (en)"),
        ("ja",    "日本語 (ja)"),
        ("ko",    "한국어 (ko)"),
        ("fr",    "Français (fr)"),
        ("de",    "Deutsch (de)"),
        ("uk",    "Українська (uk)"),
        ("es",    "Español (es)"),
        ("pt",    "Português (pt)"),
        ("eo",    "Esperanto (eo)"),
    ];

    private static string Detect()
    {
        var n = CultureInfo.CurrentUICulture.Name;
        if (n.Contains("Hant") || n.Contains("TW") || n.Contains("HK") || n.Contains("MO")) return "zh-TW";
        if (n.StartsWith("zh")) return "zh-CN";
        if (n.StartsWith("ja")) return "ja";
        if (n.StartsWith("ko")) return "ko";
        if (n.StartsWith("fr")) return "fr";
        if (n.StartsWith("de")) return "de";
        if (n.StartsWith("uk")) return "uk";
        if (n.StartsWith("es")) return "es";
        if (n.StartsWith("pt")) return "pt";
        if (n.StartsWith("eo")) return "eo";
        return "en";
    }

    // ── 查詢（fallback → en → key） ─────────────────────
    private static string T(string key)
    {
        if (_res.TryGetValue(key, out var row))
        {
            if (row.TryGetValue(Lang, out var v)) return v;
            if (row.TryGetValue("en",  out var e)) return e;
        }
        return key;
    }

    // ── UI 字型 ─────────────────────────────────────────
    public static string UiFont => Lang switch
    {
        "zh-TW" => "Microsoft JhengHei UI",
        "zh-CN" => "Microsoft YaHei UI",
        "ja"    => "Yu Gothic UI",
        "ko"    => "Malgun Gothic",
        _       => "Segoe UI"
    };

    // ── 公開屬性（直接字串） ─────────────────────────────
    public static string FormTitle       => T("FormTitle");
    public static string GroupApi        => T("GroupApi");
    public static string LblApiKey       => T("LblApiKey");
    public static string ChkShow         => T("ChkShow");
    public static string BtnSave         => T("BtnSave");
    public static string GroupSearch     => T("GroupSearch");
    public static string LblNatural      => T("LblNatural");
    public static string TxtPlaceholder  => T("TxtPlaceholder");
    public static string BtnSearch       => T("BtnSearch");
    public static string LblSyntax       => T("LblSyntax");
    public static string LblResultsEmpty => T("LblResultsEmpty");
    public static string StatusReady     => T("StatusReady");
    public static string StatusKeySaved  => T("StatusKeySaved");
    public static string StatusAsking    => T("StatusAsking");
    public static string MsgNeedQuery    => T("MsgNeedQuery");
    public static string MsgNeedKey      => T("MsgNeedKey");
    public static string MsgTitleInfo    => T("MsgTitleInfo");
    public static string MsgTitleWarn    => T("MsgTitleWarn");
    public static string MsgTitleError   => T("MsgTitleError");
    public static string NoResults       => T("NoResults");
    public static string CtxOpen         => T("CtxOpen");
    public static string CtxExplore      => T("CtxExplore");
    public static string MetaFolder      => T("MetaFolder");

    // ── 帶參數的字串 ────────────────────────────────────
    public static string LblResultsCount(int n)    => string.Format(T("LblResultsCount"),   n);
    public static string StatusSearching(string q) => string.Format(T("StatusSearching"),   q);
    public static string StatusDone(int n)         => string.Format(T("StatusDone"),         n);
    public static string StatusError(string msg)   => string.Format(T("StatusError"),        msg);

    // ── Claude 系統提示詞（固定繁體中文，完整語法參考） ─
    public static string SystemPrompt => """
        你是 Everything 搜尋語法專家。
        將使用者的自然語言檔案搜尋需求，轉換成 Everything 搜尋語法。
        只回傳搜尋語法字串，不要加任何說明、標點、引號、反引號或多餘文字。

        【重要】多個副檔名必須用分號 ; 分隔寫在同一個 ext: 內：
          正確：ext:mp4;mkv;avi size:>1gb
          錯誤：ext:mp4|mkv|avi size:>1gb  ← | 是全域 OR，會讓條件失效

        Everything 語法範例：
        - 找 mp4 且大於 1MB：ext:mp4 size:>1mb
        - 找今天修改的 txt：ext:txt dm:today
        - 找 D 槽的 jpg 且大於 500KB：path:D:\ ext:jpg size:>500kb
        - 找檔名包含「報告」的 docx：報告 ext:docx
        - 找大於 1GB 的影片：ext:mp4;mkv;avi;mov size:>1gb
        - 找本週的圖片：ext:jpg;jpeg;png;gif dm:thisweek
        - 找小於 1MB 的 pdf：ext:pdf size:<1mb
        - 找空資料夾：empty:
        - 找重複檔案：dupe:
        - 找所有影片（用巨集）：video: size:>100mb
        - 找所有圖片（用巨集）：pic: dm:today

        運算子：
        	[ ]半形空白	 及 (AND)
        	|	 或 (OR)，僅用於分隔獨立條件，不可用於 ext: 內
        	!	 非 (NOT)
        	< >	 群組
        	" "	 搜尋引號內的字詞。

        萬用字元：
        	*	 符合 0 個或多個字元。
        	?	 符合 1 個字元。

        巨集（內建分類，單一關鍵字即可）：
        	audio:  video:  pic:  doc:  zip:  exe:

        修飾元：
        	file:  folder:  path:  case:  nopath:  regex:  wfn:  wholeword:

        函數：
        	ext:<ext1;ext2;ext3>  size:<n>  dm:<date>  dc:<date>  da:<date>
        	path:<path>  parent:<path>  infolder:<path>  len:<n>
        	dupe:  empty:  sizedupe:  attrib:<flags>  content:<text>
        	dimensions:<w>x<h>  width:<n>  height:<n>  bitdepth:<n>
        	artist:  album:  title:  genre:  track:  comment:

        函數語法：func:value  func:<value  func:>value  func:<=value  func:>=value  func:start..end

        大小：size[kb|mb|gb]；常數：empty tiny small medium large huge gigantic
        日期常數：today yesterday tomorrow thisweek lastweek thismonth lastyear
        屬性：A=封存 C=壓縮 D=目錄 E=加密 H=隱藏 R=唯讀 S=系統 T=暫存

        常用副檔名參考（ext: 語法中以分號 ; 分隔多個副檔名）：
        　影片：mp4;mkv;avi;mov;wmv;flv;webm;m4v;mpg;mpeg;3gp;ts;m2ts;mts;vob;ogv;rm;rmvb;divx;xvid;hevc;f4v;asf
        　音訊：mp3;flac;wav;aac;ogg;wma;m4a;opus;aiff;aif;ape;mpc;wv;ra;mid;midi;ac3;dts;amr;mka;alac;m4b
        　圖片：jpg;jpeg;png;gif;bmp;webp;tiff;tif;heic;heif;raw;cr2;cr3;nef;arw;dng;orf;rw2;psd;psb;svg;avif;jxl
        　文件：pdf;doc;docx;xls;xlsx;ppt;pptx;odt;ods;odp;rtf;txt;md;epub;mobi;djvu;csv;tex;chm;fb2
        　壓縮：zip;rar;7z;tar;gz;bz2;xz;iso;img;cab;lz4;zst;ace;arj;wim;tgz;tbz2
        　程式碼：py;js;ts;cs;java;cpp;c;h;go;rs;rb;php;sh;bat;ps1;lua;swift;kt;sql;vue;dart;scala
        　字型：ttf;otf;woff;woff2;eot;ttc;dfont
        　執行檔：exe;msi;dll;com;apk;deb;rpm;ipa;jar;appimage
        　播放清單：m3u;m3u8;pls;asx;xspf;wpl;cue
        　字幕：srt;ass;ssa;sub;vtt;sbv;smi;sup;lrc
        　資料庫：db;sqlite;sqlite3;mdb;accdb;sql;dbf;mdf
        　設定：ini;cfg;conf;toml;yaml;yml;json;xml;plist;reg
        　電子郵件：eml;msg;mbox;pst;ost;vcf
        　3D模型：fbx;obj;stl;gltf;glb;blend;dae;3ds;c4d;ply;usdz
        　Torrent：torrent
        """;

    // ── 字串資料表 ───────────────────────────────────────
    // 欄位順序：zh-TW / zh-CN / en / ja / ko / fr / de / uk / es / pt / eo
    private static readonly Dictionary<string, Dictionary<string, string>> _res = new()
    {
        ["FormTitle"] = new()
        {
            ["zh-TW"] = "EverythingWithAI — 自然語言檔案搜尋",
            ["zh-CN"] = "EverythingWithAI — 自然语言文件搜索",
            ["en"]    = "EverythingWithAI — Natural Language File Search",
            ["ja"]    = "EverythingWithAI — 自然言語ファイル検索",
            ["ko"]    = "EverythingWithAI — 자연어 파일 검색",
            ["fr"]    = "EverythingWithAI — Recherche de fichiers en langage naturel",
            ["de"]    = "EverythingWithAI — Dateisuche in natürlicher Sprache",
            ["uk"]    = "EverythingWithAI — Пошук файлів природною мовою",
            ["es"]    = "EverythingWithAI — Búsqueda de archivos en lenguaje natural",
            ["pt"]    = "EverythingWithAI — Pesquisa de arquivos em linguagem natural",
            ["eo"]    = "EverythingWithAI — Natura-Lingva Dosierserĉado",
        },
        ["GroupApi"] = new()
        {
            ["zh-TW"] = "Claude API 設定",
            ["zh-CN"] = "Claude API 设置",
            ["en"]    = "Claude API Settings",
            ["ja"]    = "Claude API 設定",
            ["ko"]    = "Claude API 설정",
            ["fr"]    = "Paramètres Claude API",
            ["de"]    = "Claude API-Einstellungen",
            ["uk"]    = "Налаштування Claude API",
            ["es"]    = "Configuración de Claude API",
            ["pt"]    = "Configurações da Claude API",
            ["eo"]    = "Claude API-Agordoj",
        },
        ["LblApiKey"] = new()
        {
            ["zh-TW"] = "API Key：",
            ["zh-CN"] = "API Key：",
            ["en"]    = "API Key:",
            ["ja"]    = "API Key：",
            ["ko"]    = "API Key：",
            ["fr"]    = "Clé API :",
            ["de"]    = "API-Schlüssel:",
            ["uk"]    = "Ключ API:",
            ["es"]    = "Clave API:",
            ["pt"]    = "Chave API:",
            ["eo"]    = "API-Ŝlosilo:",
        },
        ["ChkShow"] = new()
        {
            ["zh-TW"] = "顯示",
            ["zh-CN"] = "显示",
            ["en"]    = "Show",
            ["ja"]    = "表示",
            ["ko"]    = "표시",
            ["fr"]    = "Afficher",
            ["de"]    = "Anzeigen",
            ["uk"]    = "Показати",
            ["es"]    = "Mostrar",
            ["pt"]    = "Mostrar",
            ["eo"]    = "Montri",
        },
        ["BtnSave"] = new()
        {
            ["zh-TW"] = "儲存",
            ["zh-CN"] = "保存",
            ["en"]    = "Save",
            ["ja"]    = "保存",
            ["ko"]    = "저장",
            ["fr"]    = "Enregistrer",
            ["de"]    = "Speichern",
            ["uk"]    = "Зберегти",
            ["es"]    = "Guardar",
            ["pt"]    = "Salvar",
            ["eo"]    = "Konservi",
        },
        ["GroupSearch"] = new()
        {
            ["zh-TW"] = "搜尋",
            ["zh-CN"] = "搜索",
            ["en"]    = "Search",
            ["ja"]    = "検索",
            ["ko"]    = "검색",
            ["fr"]    = "Recherche",
            ["de"]    = "Suche",
            ["uk"]    = "Пошук",
            ["es"]    = "Búsqueda",
            ["pt"]    = "Pesquisa",
            ["eo"]    = "Serĉo",
        },
        ["LblNatural"] = new()
        {
            ["zh-TW"] = "自然語言：",
            ["zh-CN"] = "自然语言：",
            ["en"]    = "Natural language:",
            ["ja"]    = "自然言語：",
            ["ko"]    = "자연어：",
            ["fr"]    = "Langage naturel :",
            ["de"]    = "Natürliche Sprache:",
            ["uk"]    = "Природна мова:",
            ["es"]    = "Lenguaje natural:",
            ["pt"]    = "Linguagem natural:",
            ["eo"]    = "Natura lingvo:",
        },
        ["TxtPlaceholder"] = new()
        {
            ["zh-TW"] = "例：找大於 1MB 的 mp4 影片",
            ["zh-CN"] = "例：找大于 1MB 的 mp4 视频",
            ["en"]    = "e.g. find mp4 videos larger than 1 MB",
            ["ja"]    = "例：1MB 以上の mp4 動画を探す",
            ["ko"]    = "예: 1MB 이상의 mp4 동영상 찾기",
            ["fr"]    = "ex : trouver des vidéos mp4 de plus de 1 Mo",
            ["de"]    = "z. B. mp4-Videos größer als 1 MB suchen",
            ["uk"]    = "напр.: знайти відео mp4 більше 1 МБ",
            ["es"]    = "ej.: buscar vídeos mp4 de más de 1 MB",
            ["pt"]    = "ex.: encontrar vídeos mp4 maiores que 1 MB",
            ["eo"]    = "ekz.: serĉu mp4-filmetojn pli grandajn ol 1 MB",
        },
        ["BtnSearch"] = new()
        {
            ["zh-TW"] = "搜尋",
            ["zh-CN"] = "搜索",
            ["en"]    = "Search",
            ["ja"]    = "検索",
            ["ko"]    = "검색",
            ["fr"]    = "Rechercher",
            ["de"]    = "Suchen",
            ["uk"]    = "Шукати",
            ["es"]    = "Buscar",
            ["pt"]    = "Pesquisar",
            ["eo"]    = "Serĉi",
        },
        ["LblSyntax"] = new()
        {
            ["zh-TW"] = "Everything 語法：",
            ["zh-CN"] = "Everything 语法：",
            ["en"]    = "Everything syntax:",
            ["ja"]    = "Everything 構文：",
            ["ko"]    = "Everything 구문：",
            ["fr"]    = "Syntaxe Everything :",
            ["de"]    = "Everything-Syntax:",
            ["uk"]    = "Синтаксис Everything:",
            ["es"]    = "Sintaxis de Everything:",
            ["pt"]    = "Sintaxe Everything:",
            ["eo"]    = "Everything-Sintakso:",
        },
        ["LblResultsEmpty"] = new()
        {
            ["zh-TW"] = "結果：",
            ["zh-CN"] = "结果：",
            ["en"]    = "Results:",
            ["ja"]    = "結果：",
            ["ko"]    = "결과：",
            ["fr"]    = "Résultats :",
            ["de"]    = "Ergebnisse:",
            ["uk"]    = "Результати:",
            ["es"]    = "Resultados:",
            ["pt"]    = "Resultados:",
            ["eo"]    = "Rezultoj:",
        },
        ["LblResultsCount"] = new()
        {
            ["zh-TW"] = "結果：共 {0} 筆",
            ["zh-CN"] = "结果：共 {0} 项",
            ["en"]    = "Results: {0} items",
            ["ja"]    = "結果：{0} 件",
            ["ko"]    = "결과：{0} 개",
            ["fr"]    = "Résultats : {0} élément(s)",
            ["de"]    = "Ergebnisse: {0}",
            ["uk"]    = "Результати: {0} елемент(ів)",
            ["es"]    = "Resultados: {0} elemento(s)",
            ["pt"]    = "Resultados: {0} item(ns)",
            ["eo"]    = "Rezultoj: {0} ero(j)",
        },
        ["StatusReady"] = new()
        {
            ["zh-TW"] = "就緒",
            ["zh-CN"] = "就绪",
            ["en"]    = "Ready",
            ["ja"]    = "準備完了",
            ["ko"]    = "준비",
            ["fr"]    = "Prêt",
            ["de"]    = "Bereit",
            ["uk"]    = "Готово",
            ["es"]    = "Listo",
            ["pt"]    = "Pronto",
            ["eo"]    = "Preta",
        },
        ["StatusKeySaved"] = new()
        {
            ["zh-TW"] = "API Key 已儲存。",
            ["zh-CN"] = "API Key 已保存。",
            ["en"]    = "API Key saved.",
            ["ja"]    = "API Key を保存しました。",
            ["ko"]    = "API Key 저장됨.",
            ["fr"]    = "Clé API enregistrée.",
            ["de"]    = "API-Schlüssel gespeichert.",
            ["uk"]    = "Ключ API збережено.",
            ["es"]    = "Clave API guardada.",
            ["pt"]    = "Chave API salva.",
            ["eo"]    = "API-ŝlosilo konservita.",
        },
        ["StatusAsking"] = new()
        {
            ["zh-TW"] = "正在詢問 Claude 轉換語法…",
            ["zh-CN"] = "正在询问 Claude 转换语法…",
            ["en"]    = "Asking Claude to convert query…",
            ["ja"]    = "Claude に変換を問い合わせています…",
            ["ko"]    = "Claude에 구문 변환 요청 중…",
            ["fr"]    = "Interrogation de Claude en cours…",
            ["de"]    = "Claude wird befragt…",
            ["uk"]    = "Запитуємо Claude для конвертації запиту…",
            ["es"]    = "Consultando a Claude para la conversión…",
            ["pt"]    = "Consultando Claude para converter…",
            ["eo"]    = "Demandante Claude konverti…",
        },
        ["StatusSearching"] = new()
        {
            ["zh-TW"] = "Everything 語法：{0}　正在搜尋…",
            ["zh-CN"] = "Everything 语法：{0}　正在搜索…",
            ["en"]    = "Everything syntax: {0} — Searching…",
            ["ja"]    = "Everything 構文：{0}　検索中…",
            ["ko"]    = "Everything 구문：{0}　검색 중…",
            ["fr"]    = "Syntaxe Everything : {0} — Recherche…",
            ["de"]    = "Everything-Syntax: {0} — Suche läuft…",
            ["uk"]    = "Синтаксис Everything: {0} — Пошук…",
            ["es"]    = "Sintaxis de Everything: {0} — Buscando…",
            ["pt"]    = "Sintaxe Everything: {0} — Pesquisando…",
            ["eo"]    = "Everything-sintakso: {0} — Serĉante…",
        },
        ["StatusDone"] = new()
        {
            ["zh-TW"] = "搜尋完成，共找到 {0} 筆。（點擊可開啟）",
            ["zh-CN"] = "搜索完成，共找到 {0} 项。（点击可打开）",
            ["en"]    = "Search complete — {0} result(s). (Click to open)",
            ["ja"]    = "検索完了 — {0} 件見つかりました。（クリックで開く）",
            ["ko"]    = "검색 완료 — {0}개 결과. (클릭하여 열기)",
            ["fr"]    = "Recherche terminée — {0} résultat(s). (Cliquez pour ouvrir)",
            ["de"]    = "Suche abgeschlossen — {0} Ergebnis(se). (Klicken zum Öffnen)",
            ["uk"]    = "Пошук завершено — {0} результат(ів). (Натисніть, щоб відкрити)",
            ["es"]    = "Búsqueda completada — {0} resultado(s). (Clic para abrir)",
            ["pt"]    = "Pesquisa concluída — {0} resultado(s). (Clique para abrir)",
            ["eo"]    = "Serĉo finita — {0} rezulto(j). (Alklaku por malfermi)",
        },
        ["StatusError"] = new()
        {
            ["zh-TW"] = "錯誤：{0}",
            ["zh-CN"] = "错误：{0}",
            ["en"]    = "Error: {0}",
            ["ja"]    = "エラー：{0}",
            ["ko"]    = "오류：{0}",
            ["fr"]    = "Erreur : {0}",
            ["de"]    = "Fehler: {0}",
            ["uk"]    = "Помилка: {0}",
            ["es"]    = "Error: {0}",
            ["pt"]    = "Erro: {0}",
            ["eo"]    = "Eraro: {0}",
        },
        ["MsgNeedQuery"] = new()
        {
            ["zh-TW"] = "請輸入搜尋內容。",
            ["zh-CN"] = "请输入搜索内容。",
            ["en"]    = "Please enter a search query.",
            ["ja"]    = "検索内容を入力してください。",
            ["ko"]    = "검색 내용을 입력하세요.",
            ["fr"]    = "Veuillez entrer une requête de recherche.",
            ["de"]    = "Bitte geben Sie einen Suchbegriff ein.",
            ["uk"]    = "Будь ласка, введіть пошуковий запит.",
            ["es"]    = "Por favor, introduce una consulta de búsqueda.",
            ["pt"]    = "Por favor, insira uma consulta de pesquisa.",
            ["eo"]    = "Bonvolu enigi serĉan peton.",
        },
        ["MsgNeedKey"] = new()
        {
            ["zh-TW"] = "請先輸入 Claude API Key。",
            ["zh-CN"] = "请先输入 Claude API Key。",
            ["en"]    = "Please enter your Claude API Key first.",
            ["ja"]    = "先に Claude API Key を入力してください。",
            ["ko"]    = "먼저 Claude API Key를 입력하세요.",
            ["fr"]    = "Veuillez d'abord saisir votre clé Claude API.",
            ["de"]    = "Bitte geben Sie zuerst Ihren Claude API-Schlüssel ein.",
            ["uk"]    = "Будь ласка, спочатку введіть ключ Claude API.",
            ["es"]    = "Por favor, introduce primero tu clave de Claude API.",
            ["pt"]    = "Por favor, insira primeiro sua chave Claude API.",
            ["eo"]    = "Bonvolu unue enigi vian Claude API-ŝlosilon.",
        },
        ["MsgTitleInfo"] = new()
        {
            ["zh-TW"] = "提示",
            ["zh-CN"] = "提示",
            ["en"]    = "Info",
            ["ja"]    = "情報",
            ["ko"]    = "알림",
            ["fr"]    = "Information",
            ["de"]    = "Hinweis",
            ["uk"]    = "Інформація",
            ["es"]    = "Información",
            ["pt"]    = "Informação",
            ["eo"]    = "Informo",
        },
        ["MsgTitleWarn"] = new()
        {
            ["zh-TW"] = "提示",
            ["zh-CN"] = "提示",
            ["en"]    = "Warning",
            ["ja"]    = "警告",
            ["ko"]    = "경고",
            ["fr"]    = "Avertissement",
            ["de"]    = "Warnung",
            ["uk"]    = "Попередження",
            ["es"]    = "Advertencia",
            ["pt"]    = "Aviso",
            ["eo"]    = "Averto",
        },
        ["MsgTitleError"] = new()
        {
            ["zh-TW"] = "錯誤",
            ["zh-CN"] = "错误",
            ["en"]    = "Error",
            ["ja"]    = "エラー",
            ["ko"]    = "오류",
            ["fr"]    = "Erreur",
            ["de"]    = "Fehler",
            ["uk"]    = "Помилка",
            ["es"]    = "Error",
            ["pt"]    = "Erro",
            ["eo"]    = "Eraro",
        },
        ["NoResults"] = new()
        {
            ["zh-TW"] = "無搜尋結果",
            ["zh-CN"] = "无搜索结果",
            ["en"]    = "No results found",
            ["ja"]    = "検索結果がありません",
            ["ko"]    = "검색 결과 없음",
            ["fr"]    = "Aucun résultat",
            ["de"]    = "Keine Ergebnisse",
            ["uk"]    = "Результатів не знайдено",
            ["es"]    = "Sin resultados",
            ["pt"]    = "Sem resultados",
            ["eo"]    = "Neniu rezulto",
        },
        ["CtxOpen"] = new()
        {
            ["zh-TW"] = "開啟檔案",
            ["zh-CN"] = "打开文件",
            ["en"]    = "Open file",
            ["ja"]    = "ファイルを開く",
            ["ko"]    = "파일 열기",
            ["fr"]    = "Ouvrir le fichier",
            ["de"]    = "Datei öffnen",
            ["uk"]    = "Відкрити файл",
            ["es"]    = "Abrir archivo",
            ["pt"]    = "Abrir arquivo",
            ["eo"]    = "Malfermi dosieron",
        },
        ["CtxExplore"] = new()
        {
            ["zh-TW"] = "在檔案總管中顯示",
            ["zh-CN"] = "在文件资源管理器中显示",
            ["en"]    = "Show in Explorer",
            ["ja"]    = "エクスプローラーで表示",
            ["ko"]    = "탐색기에서 표시",
            ["fr"]    = "Afficher dans l'Explorateur",
            ["de"]    = "Im Explorer anzeigen",
            ["uk"]    = "Відкрити у Провіднику",
            ["es"]    = "Mostrar en el Explorador",
            ["pt"]    = "Mostrar no Explorador",
            ["eo"]    = "Montri en dosierumilo",
        },
        ["MetaFolder"] = new()
        {
            ["zh-TW"] = "[資料夾]",
            ["zh-CN"] = "[文件夹]",
            ["en"]    = "[Folder]",
            ["ja"]    = "[フォルダー]",
            ["ko"]    = "[폴더]",
            ["fr"]    = "[Dossier]",
            ["de"]    = "[Ordner]",
            ["uk"]    = "[Папка]",
            ["es"]    = "[Carpeta]",
            ["pt"]    = "[Pasta]",
            ["eo"]    = "[Dosierujo]",
        },
    };
}
