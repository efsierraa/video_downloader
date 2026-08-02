// Video Downloader.exe -- simple Windows app for downloading videos
// from Facebook and YouTube (graphical front-end for yt-dlp).
//
// Double-click it, paste the video URL, click Download.
//
// Command line (optional, used for unattended downloads and testing):
//   "Video Downloader.exe" --url <url> --out <dir> --quality best|1080p|720p|480p|mp3 --auto
//   --theme light|dark   forces a theme (default: follow the Windows setting)
//
// Compile (no Visual Studio needed):
//   .\Build-App.ps1
// or manually:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
//     /out:"Video Downloader.exe" /r:System.dll /r:System.Drawing.dll ^
//     /r:System.Windows.Forms.dll "Video Downloader.cs"

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

class VideoDownloaderApp : Form
{
    static readonly string ExeDir = Path.GetDirectoryName(
        System.Reflection.Assembly.GetExecutingAssembly().Location);

    static readonly string YtDlp   = Path.Combine(ExeDir, "yt-dlp.exe");
    static readonly string Deno    = Path.Combine(ExeDir, "deno.exe");
    static readonly string Ffmpeg  = Path.Combine(ExeDir, "ffmpeg.exe");
    static readonly string Ffprobe = Path.Combine(ExeDir, "ffprobe.exe");

    static readonly string[] QualityLabels = new string[] {
        "Best (default)", "1080p", "720p", "480p", "Audio only (MP3)"
    };

    static readonly Regex RxProgress = new Regex(
        @"\[download\]\s+(\d+(?:\.\d+)?)%.*?ETA\s+(\d+:\d+)", RegexOptions.Compiled);
    static readonly Regex RxProgressSimple = new Regex(
        @"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    // ---- UI controls -------------------------------------------------------
    AppleTextBox txtUrl;
    AppleTextBox txtOut;
    AppleComboBox cmbQuality;
    AppleButton btnBrowse;
    AppleButton btnDownload;
    AppleButton btnUpdate;
    AppleButton btnFbLogin;
    AppleProgressBar progress;
    Label lblStatus;
    Label lblVersion;
    TextBox txtLog;

    // ---- Layout state ------------------------------------------------------
    TableLayoutPanel layout;
    Panel logCard;
    Label lblDetails;
    int logRowIndex;
    bool showDetails;
    int compactHeight;
    bool heightRecorded;

    // ---- State -------------------------------------------------------------
    string defaultOutDir;
    bool autoMode;
    string autoUrl;
    string autoOut;
    int autoQualityIdx;
    Process currentProcess;

    [STAThread]
    static int Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Theme.Init(args);
        VideoDownloaderApp form = new VideoDownloaderApp();
        form.ParseArgs(args);
        Application.Run(form);
        return Environment.ExitCode;
    }

    public VideoDownloaderApp()
    {
        defaultOutDir = Path.Combine(ExeDir, "downloads");
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }
        BuildUi();
        Log("Ready. Paste a video URL and click Download.");
    }

    // ========================================================================
    //  UI construction (Apple-style, responsive)
    // ========================================================================
    void BuildUi()
    {
        Font = new Font("Segoe UI", 9.75F);
        Text = "Video Downloader";
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        ClientSize = new Size(600, 432);
        MinimumSize = new Size(520, 432);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;

        layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(18, 12, 18, 10);
        layout.BackColor = Theme.Bg;
        layout.ColumnCount = 1;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(layout);
        int row = 0;

        // ---- header ---------------------------------------------------------
        Label lblTitle = new Label();
        lblTitle.Text = "Video Downloader";
        lblTitle.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold);
        lblTitle.ForeColor = Theme.Text;
        lblTitle.AutoSize = true;
        lblTitle.Margin = new Padding(2, 4, 0, 0);
        layout.Controls.Add(lblTitle, 0, row++);

        Label lblSubtitle = new Label();
        lblSubtitle.Text = "Paste a link from Facebook or YouTube";
        lblSubtitle.ForeColor = Theme.Sub;
        lblSubtitle.AutoSize = true;
        lblSubtitle.Margin = new Padding(2, 0, 0, 10);
        layout.Controls.Add(lblSubtitle, 0, row++);

        // ---- fields ----------------------------------------------------------
        layout.Controls.Add(Caption("VIDEO URL"), 0, row++);

        txtUrl = new AppleTextBox();
        txtUrl.Dock = DockStyle.Fill;
        txtUrl.Margin = new Padding(0, 0, 0, 4);
        layout.Controls.Add(txtUrl, 0, row++);

        layout.Controls.Add(Caption("SAVE TO"), 0, row++);

        TableLayoutPanel saveRow = new TableLayoutPanel();
        saveRow.Dock = DockStyle.Fill;
        saveRow.AutoSize = true;
        saveRow.Margin = new Padding(0, 0, 0, 4);
        saveRow.BackColor = Theme.Bg;
        saveRow.ColumnCount = 2;
        saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        saveRow.RowCount = 1;
        txtOut = new AppleTextBox();
        txtOut.Dock = DockStyle.Fill;
        txtOut.Text = defaultOutDir;
        txtOut.Margin = new Padding(0, 0, 8, 0);
        saveRow.Controls.Add(txtOut, 0, 0);
        btnBrowse = new AppleButton();
        btnBrowse.Text = "Browse...";
        btnBrowse.Size = new Size(96, 30);
        btnBrowse.Margin = new Padding(0);
        btnBrowse.Click += btnBrowse_Click;
        saveRow.Controls.Add(btnBrowse, 1, 0);
        layout.Controls.Add(saveRow, 0, row++);

        layout.Controls.Add(Caption("QUALITY"), 0, row++);

        cmbQuality = new AppleComboBox();
        cmbQuality.Items.AddRange(QualityLabels);
        cmbQuality.SelectedIndex = 0;
        cmbQuality.Size = new Size(220, 30);
        cmbQuality.Margin = new Padding(0, 0, 0, 4);
        cmbQuality.Anchor = AnchorStyles.Left;
        layout.Controls.Add(cmbQuality, 0, row++);

        // ---- download ----------------------------------------------------------
        btnDownload = new AppleButton();
        btnDownload.Text = "Download";
        btnDownload.Accent = true;
        btnDownload.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnDownload.Dock = DockStyle.Fill;
        btnDownload.Height = 44;
        btnDownload.Margin = new Padding(0, 10, 0, 10);
        btnDownload.Click += btnDownload_Click;
        layout.Controls.Add(btnDownload, 0, row++);
        AcceptButton = btnDownload;

        progress = new AppleProgressBar();
        progress.Dock = DockStyle.Fill;
        progress.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(progress, 0, row++);

        lblStatus = new Label();
        lblStatus.AutoSize = false;
        lblStatus.Height = 20;
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.AutoEllipsis = true;
        lblStatus.ForeColor = Theme.Sub;
        lblStatus.Margin = new Padding(2, 0, 0, 4);
        lblStatus.Text = "";
        layout.Controls.Add(lblStatus, 0, row++);

        // ---- collapsible details --------------------------------------------
        lblDetails = new Label();
        lblDetails.Text = "Show details ▾";
        lblDetails.ForeColor = Theme.Sub;
        lblDetails.AutoSize = true;
        lblDetails.Cursor = Cursors.Hand;
        lblDetails.Margin = new Padding(2, 0, 0, 6);
        lblDetails.Click += delegate { ToggleDetails(); };
        lblDetails.MouseEnter += delegate { lblDetails.Font = new Font(lblDetails.Font, FontStyle.Underline); };
        lblDetails.MouseLeave += delegate { lblDetails.Font = new Font(lblDetails.Font, FontStyle.Regular); };
        layout.Controls.Add(lblDetails, 0, row++);

        logCard = new Panel();
        logCard.BackColor = Theme.Card;
        logCard.Padding = new Padding(8);
        logCard.Dock = DockStyle.Fill;
        logCard.Margin = new Padding(0);
        logCard.Visible = false;
        txtLog = new TextBox();
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Font = new Font("Consolas", 8.5F);
        txtLog.BackColor = Theme.Card;
        txtLog.ForeColor = Theme.Sub;
        txtLog.BorderStyle = BorderStyle.None;
        txtLog.Dock = DockStyle.Fill;
        logCard.Controls.Add(txtLog);
        logRowIndex = row;
        layout.Controls.Add(logCard, 0, row++);

        // ---- footer ----------------------------------------------------------
        TableLayoutPanel footer = new TableLayoutPanel();
        footer.Dock = DockStyle.Fill;
        footer.AutoSize = true;
        footer.Margin = new Padding(0, 8, 0, 0);
        footer.BackColor = Theme.Bg;
        footer.ColumnCount = 2;
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowCount = 1;

        lblVersion = new Label();
        lblVersion.Text = "yt-dlp ...";
        lblVersion.ForeColor = Theme.Sub;
        lblVersion.Font = new Font("Segoe UI", 8.25F);
        lblVersion.AutoSize = true;
        lblVersion.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        footer.Controls.Add(lblVersion, 0, 0);

        FlowLayoutPanel footerButtons = new FlowLayoutPanel();
        footerButtons.FlowDirection = FlowDirection.LeftToRight;
        footerButtons.AutoSize = true;
        footerButtons.Margin = new Padding(0);
        footerButtons.Padding = new Padding(0);
        footerButtons.WrapContents = false;
        footerButtons.BackColor = Theme.Bg;

        btnFbLogin = new AppleButton();
        btnFbLogin.Size = new Size(170, 30);
        btnFbLogin.Margin = new Padding(0, 0, 8, 0);
        btnFbLogin.Click += btnFbLogin_Click;
        footerButtons.Controls.Add(btnFbLogin);

        btnUpdate = new AppleButton();
        btnUpdate.Text = "Update yt-dlp";
        btnUpdate.Size = new Size(120, 30);
        btnUpdate.Margin = new Padding(0);
        btnUpdate.Click += btnUpdate_Click;
        footerButtons.Controls.Add(btnUpdate);

        footer.Controls.Add(footerButtons, 1, 0);
        layout.Controls.Add(footer, 0, row++);

        // All rows auto-size to their content, except the log row which
        // fills the remaining space when expanded (and is 0 when collapsed).
        layout.RowCount = row;
        for (int i = 0; i < row; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles[logRowIndex] = new RowStyle(SizeType.Absolute, 0F);

        ActiveControl = btnDownload; // keep focus out of the text fields
        UpdateFbLoginButton();
    }

    Label Caption(string text)
    {
        Label l = new Label();
        l.Text = text;
        l.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        l.ForeColor = Theme.Sub;
        l.AutoSize = true;
        l.Margin = new Padding(2, 6, 0, 3);
        return l;
    }

    void ToggleDetails()
    {
        showDetails = !showDetails;
        logCard.Visible = showDetails;
        layout.RowStyles[logRowIndex] = showDetails
            ? new RowStyle(SizeType.Percent, 100F)
            : new RowStyle(SizeType.Absolute, 0F);
        lblDetails.Text = showDetails ? "Hide details ▴" : "Show details ▾";
        const int grow = 180;
        if (showDetails)
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + grow);
        else
            ClientSize = new Size(ClientSize.Width, compactHeight);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBarTheme(Handle);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (!heightRecorded && !showDetails && ClientSize.Height > 0)
        {
            heightRecorded = true;
            compactHeight = ClientSize.Height;
        }
    }

    // ========================================================================
    //  Startup / command line
    // ========================================================================
    void ParseArgs(string[] args)
    {
        autoQualityIdx = 0;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--auto")
                autoMode = true;
            else if (a == "--url" && i + 1 < args.Length)
                autoUrl = args[++i];
            else if (a == "--out" && i + 1 < args.Length)
                autoOut = args[++i];
            else if (a == "--quality" && i + 1 < args.Length)
                autoQualityIdx = QualityIndex(args[++i]);
        }

        if (autoUrl != null) txtUrl.Text = autoUrl;
        if (autoOut != null) txtOut.Text = autoOut;
        cmbQuality.SelectedIndex = autoQualityIdx;
    }

    static int QualityIndex(string q)
    {
        q = (q ?? "").Trim().ToLowerInvariant();
        if (q == "1080p" || q == "1080") return 1;
        if (q == "720p"  || q == "720")  return 2;
        if (q == "480p"  || q == "480")  return 3;
        if (q == "mp3"   || q == "audio") return 4;
        return 0;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        EnableInputs(false);
        SetStatus("Checking required files...");

        RunBackground(delegate
        {
            // The app never downloads anything itself: every tool it needs
            // ships next to the exe in the release zip. (The only exception
            // is "Update yt-dlp", where yt-dlp updates itself.)
            string missing = MissingFiles("yt-dlp.exe", "deno.exe", "ffmpeg.exe", "ffprobe.exe");
            if (missing != null)
            {
                UI(delegate
                {
                    if (autoMode)
                    {
                        Log("ERROR: required files are missing: " + missing);
                        Environment.ExitCode = 1;
                        Close();
                        return;
                    }
                    SetStatus("Required files are missing.");
                    Log("ERROR: required files are missing: " + missing);
                    MessageBox.Show(this,
                        "Some required files are missing from this folder:\r\n\r\n" + missing +
                        "\r\n\r\nPlease re-download the complete package:\r\n" +
                        "https://github.com/efsierraa/video_downloader/releases",
                        "Video Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                });
                return;
            }

            string version = GetYtDlpVersion();
            UI(delegate
            {
                lblVersion.Text = "yt-dlp " + version;
                SetStatus("Ready.");
            });

            if (autoMode)
            {
                DoAutoDownload();
            }
            else
            {
                UI(delegate { EnableInputs(true); });
            }
        });
    }

    // Returns a comma-separated list of the given files that are NOT next
    // to the exe, or null if all of them exist.
    static string MissingFiles(params string[] names)
    {
        string missing = "";
        foreach (string n in names)
            if (!File.Exists(Path.Combine(ExeDir, n)))
                missing += (missing.Length > 0 ? ", " : "") + n;
        return missing.Length > 0 ? missing : null;
    }

    void DoAutoDownload()
    {
        string url = (autoUrl ?? "").Trim().Trim('"');
        if (url.Length == 0 || url.IndexOf("http", StringComparison.OrdinalIgnoreCase) != 0)
        {
            Log("ERROR: no valid URL given for --auto mode.");
            UI(delegate { Environment.ExitCode = 2; Close(); });
            return;
        }

        string outDir = string.IsNullOrWhiteSpace(autoOut) ? defaultOutDir : autoOut;
        int code = RunDownload(url, outDir, FormatArgsFor(autoQualityIdx));

        UI(delegate { Environment.ExitCode = code; Close(); });
    }

    // ========================================================================
    //  Button handlers
    // ========================================================================
    void btnBrowse_Click(object sender, EventArgs e)
    {
        using (SaveFileDialog dlg = new SaveFileDialog())
        {
            dlg.Title = "Save video as...";
            dlg.Filter = "Video files (*.mp4;*.mkv;*.webm;*.avi)|*.mp4;*.mkv;*.webm;*.avi|All files (*.*)|*.*";
            dlg.DefaultExt = "mp4";
            string cur = txtOut.Text.Trim();
            if (Directory.Exists(cur))
                dlg.InitialDirectory = cur;
            else if (Directory.Exists(Path.GetDirectoryName(cur)))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(cur);
                dlg.FileName = Path.GetFileName(cur);
            }
            else
                dlg.InitialDirectory = defaultOutDir;
            if (dlg.ShowDialog(this) == DialogResult.OK)
                txtOut.Text = dlg.FileName;
        }
    }

    void btnDownload_Click(object sender, EventArgs e)
    {
        string url = txtUrl.Text.Trim().Trim('"');
        if (url.Length == 0 || url.IndexOf("http", StringComparison.OrdinalIgnoreCase) != 0)
        {
            MessageBox.Show(this, "Please paste a video URL first.", "Video Downloader",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string outDir = txtOut.Text;
        string formatArgs = FormatArgsFor(cmbQuality.SelectedIndex);

        EnableInputs(false);
        Log("Downloading: " + url);
        Log("Saving to:   " + outDir);

        RunBackground(delegate
        {
            int code = RunDownload(url, outDir, formatArgs);
            UI(delegate { FinishDownload(code, outDir); });
        });
    }

    void btnUpdate_Click(object sender, EventArgs e)
    {
        EnableInputs(false);
        SetStatus("Updating yt-dlp...");
        Log("Updating yt-dlp...");

        RunBackground(delegate
        {
            int code = RunYtDlp("-U");
            string version = GetYtDlpVersion();
            UI(delegate
            {
                lblVersion.Text = "yt-dlp " + version;
                SetStatus(code == 0 ? "Update finished." : "Update failed (exit code " + code + ").");
                SetProgress(0);
                EnableInputs(true);
            });
        });
    }

    // ---- Facebook login (cookies for videos that need a login) -----------
    void UpdateFbLoginButton()
    {
        string cookies = Path.Combine(ExeDir, "cookies.txt");
        btnFbLogin.Text = File.Exists(cookies) ? "Facebook login (saved)" : "Facebook login...";
    }

    void btnFbLogin_Click(object sender, EventArgs e)
    {
        string cookies = Path.Combine(ExeDir, "cookies.txt");
        string userData = Path.Combine(ExeDir, "webview2-data");
        string missing = MissingFiles("Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll", "WebView2Loader.dll");
        if (missing != null)
        {
            MessageBox.Show(this,
                "The login window needs these files, which are missing:\r\n\r\n" + missing +
                "\r\n\r\nPlease re-download the complete package:\r\n" +
                "https://github.com/efsierraa/video_downloader/releases",
                "Video Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try
        {
            using (FacebookLoginForm f = new FacebookLoginForm(cookies, userData))
                f.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not open the login window:\r\n\r\n" + ex.Message +
                "\r\n\r\n(Microsoft WebView2 Runtime is required.)",
                "Video Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateFbLoginButton();
    }

    void FinishDownload(int code, string outDir)
    {
        EnableInputs(true);
        if (code == 0)
        {
            SetProgress(100);
            SetStatus("Download completed.");
            Log("Download completed successfully.");
            DialogResult r = MessageBox.Show(this,
                "Download completed successfully.\r\n\r\nOpen the folder?",
                "Video Downloader", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes)
                Process.Start("explorer.exe", "\"" + outDir + "\"");
        }
        else
        {
            SetStatus("Download failed (exit code " + code + ").");
            Log("Download failed (exit code " + code + ").");
            MessageBox.Show(this,
                "Download failed (exit code " + code + ").\r\n\r\nSee the log for details.",
                "Video Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            Process p = currentProcess;
            if (p != null && !p.HasExited) p.Kill();
        }
        catch { }
        base.OnFormClosing(e);
    }

    // ========================================================================
    //  Download logic
    // ========================================================================
    static string FormatArgsFor(int qualityIndex)
    {
        switch (qualityIndex)
        {
            case 1:  return "--format \"bv*[height<=1080]+ba/b[height<=1080]\"";
            case 2:  return "--format \"bv*[height<=720]+ba/b[height<=720]\"";
            case 3:  return "--format \"bv*[height<=480]+ba/b[height<=480]\"";
            case 4:  return "-x --audio-format mp3 --audio-quality 0 --format \"ba/b\"";
            default: return "--format \"bv*+ba/b\"";
        }
    }

    int RunDownload(string url, string outDir, string formatArgs)
    {
        string outTemplate;
        if (Path.GetExtension(outDir).Length > 0)
        {
            outTemplate = Path.Combine(Path.GetDirectoryName(outDir),
                Path.GetFileNameWithoutExtension(outDir) + ".%(ext)s");
            outDir = Path.GetDirectoryName(outDir);
        }
        else
        {
            outTemplate = Path.Combine(outDir, "%(title)s.%(ext)s");
        }
        Directory.CreateDirectory(outDir);

        StringBuilder sb = new StringBuilder();
        if (File.Exists(Deno))
            sb.Append("--js-runtimes \"deno:").Append(Deno).Append("\" ");
        if (File.Exists(Ffmpeg))
            sb.Append("--ffmpeg-location \"").Append(ExeDir).Append("\" ");
        string cookies = Path.Combine(ExeDir, "cookies.txt");
        if (File.Exists(cookies))
            sb.Append("--cookies \"").Append(cookies).Append("\" ");
        sb.Append(formatArgs).Append(' ');
        sb.Append("--output \"").Append(outTemplate).Append("\" ");
        sb.Append("\"").Append(url).Append("\"");

        SetStatus("Starting download...");
        SetProgress(0);

        return RunYtDlp(sb.ToString());
    }

    int RunYtDlp(string arguments)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = YtDlp;
        psi.Arguments = arguments;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        try
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
        }
        catch { }

        StringBuilder errBuf = new StringBuilder();
        Process p = new Process();
        p.StartInfo = psi;
        p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                lock (errBuf) { errBuf.AppendLine(e.Data); }
        };

        p.Start();
        currentProcess = p;
        p.BeginErrorReadLine();

        // yt-dlp writes progress with '\r' instead of newlines, so the
        // output is read character by character and split on both.
        StringBuilder line = new StringBuilder();
        char[] buf = new char[512];
        int n;
        StreamReader stdout = p.StandardOutput;
        while ((n = stdout.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c == '\r' || c == '\n')
                {
                    if (line.Length > 0)
                    {
                        HandleOutputLine(line.ToString());
                        line.Length = 0;
                    }
                }
                else
                {
                    line.Append(c);
                }
            }
        }
        if (line.Length > 0)
            HandleOutputLine(line.ToString());

        p.WaitForExit();
        currentProcess = null;

        string errs;
        lock (errBuf) { errs = errBuf.ToString().Trim(); }
        if (p.ExitCode != 0 && errs.Length > 0)
            Log(errs);

        return p.ExitCode;
    }

    void HandleOutputLine(string raw)
    {
        string s = raw.Trim();
        if (s.Length == 0) return;

        Match m = RxProgress.Match(s);
        if (m.Success)
        {
            SetProgress(ParsePercent(m.Groups[1].Value));
            SetStatus("Downloading... " + m.Groups[1].Value + "%   ETA " + m.Groups[2].Value);
            return;
        }

        m = RxProgressSimple.Match(s);
        if (m.Success)
        {
            SetProgress(ParsePercent(m.Groups[1].Value));
            SetStatus("Downloading... " + m.Groups[1].Value + "%");
            return;
        }

        if (s.StartsWith("[Merger]"))
        {
            SetProgress(100);
            SetStatus("Merging video and audio...");
            Log("Merging video and audio...");
            return;
        }

        if (s.StartsWith("[ExtractAudio]"))
        {
            SetProgress(100);
            SetStatus("Extracting audio (MP3)...");
            Log("Extracting audio (MP3)...");
            return;
        }

        if (s.StartsWith("[download]") && s.Contains("%"))
            return; // leftover progress fragments, already shown on the bar

        Log(s);
    }

    static double ParsePercent(string text)
    {
        double v;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return v;
        return 0;
    }

    string GetYtDlpVersion()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(YtDlp, "--version");
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            Process p = Process.Start(psi);
            string v = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return v.Length > 0 ? v : "unknown";
        }
        catch { return "unknown"; }
    }

    // ========================================================================
    //  UI helpers (safe to call from any thread)
    // ========================================================================
    void RunBackground(ThreadStart work)
    {
        Thread t = new Thread(new ThreadStart(delegate
        {
            try { work(); }
            catch (Exception ex)
            {
                UI(delegate
                {
                    Log("ERROR: " + ex.Message);
                    SetStatus("Error: " + ex.Message);
                    if (autoMode)
                    {
                        Environment.ExitCode = 1;
                        Close();
                        return;
                    }
                    MessageBox.Show(this, "Something went wrong:\r\n\r\n" + ex.Message,
                        "Video Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    EnableInputs(true);
                });
            }
        }));
        t.IsBackground = true;
        t.Start();
    }

    void UI(MethodInvoker action)
    {
        try
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch { }
    }

    void SetStatus(string text)
    {
        UI(delegate { lblStatus.Text = text; });
    }

    void SetProgress(double percent)
    {
        UI(delegate
        {
            int v = (int)(percent * 10);
            if (v < 0) v = 0;
            if (v > progress.Maximum) v = progress.Maximum;
            progress.Value = v;
        });
    }

    void Log(string message)
    {
        UI(delegate
        {
            try
            {
                if (txtLog.TextLength > 30000)
                    txtLog.Text = txtLog.Text.Substring(15000);
                txtLog.AppendText(message + "\r\n");
            }
            catch { }
        });
    }

    void EnableInputs(bool enabled)
    {
        txtUrl.Enabled = enabled;
        btnBrowse.Enabled = enabled;
        cmbQuality.Enabled = enabled;
        btnDownload.Enabled = enabled;
        btnUpdate.Enabled = enabled;
        btnFbLogin.Enabled = enabled;
    }
}

// ============================================================================
//  Small window with a real browser (WebView2) where the user logs in to
//  Facebook. The cookies are saved as cookies.txt (Netscape format) so
//  yt-dlp can download videos that require a login.
// ============================================================================
class FacebookLoginForm : Form
{
    WebView2 web;
    AppleButton btnSave;
    Label lblHint;
    string cookiesPath;
    string userDataFolder;

    public FacebookLoginForm(string cookiesPath, string userDataFolder)
    {
        this.cookiesPath = cookiesPath;
        this.userDataFolder = userDataFolder;

        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }

        Text = "Facebook login";
        ClientSize = new Size(860, 640);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        Panel bottom = new Panel();
        bottom.Dock = DockStyle.Bottom;
        bottom.Height = 46;
        bottom.BackColor = Theme.Bg;
        bottom.Padding = new Padding(0, 8, 8, 8);
        Controls.Add(bottom);

        lblHint = new Label();
        lblHint.AutoSize = false;
        lblHint.Dock = DockStyle.Fill;
        lblHint.TextAlign = ContentAlignment.MiddleLeft;
        lblHint.Padding = new Padding(8, 0, 0, 0);
        lblHint.ForeColor = Theme.Text;
        lblHint.BackColor = Theme.Bg;
        lblHint.Text = "Loading Facebook...";
        bottom.Controls.Add(lblHint);

        btnSave = new AppleButton();
        btnSave.Accent = true;
        btnSave.Text = "Save cookies && Close";
        btnSave.Dock = DockStyle.Right;
        btnSave.Width = 170;
        btnSave.Enabled = false;
        btnSave.Click += btnSave_Click;
        bottom.Controls.Add(btnSave);

        web = new WebView2();
        web.Dock = DockStyle.Fill;
        Controls.Add(web);
        web.BringToFront();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBarTheme(Handle);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            CoreWebView2Environment env =
                await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Navigate("https://www.facebook.com/");
            lblHint.Text = "Log in to Facebook above, then click 'Save cookies && Close'.";
            btnSave.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not start the browser view:\r\n\r\n" + ex.Message,
                "Facebook login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    async void btnSave_Click(object sender, EventArgs e)
    {
        btnSave.Enabled = false;
        try
        {
            var all = new System.Collections.Generic.Dictionary<string, CoreWebView2Cookie>();
            string[] uris = new string[] {
                "https://www.facebook.com/", "https://m.facebook.com/", "https://web.facebook.com/"
            };
            foreach (string uri in uris)
            {
                var list = await web.CoreWebView2.CookieManager.GetCookiesAsync(uri);
                foreach (var c in list)
                    all[c.Domain + "|" + c.Name] = c;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Netscape HTTP Cookie File");
            bool loggedIn = false;
            foreach (var c in all.Values)
            {
                if (c.Name == "c_user") loggedIn = true;
                long exp = 0;
                if (c.Expires > DateTime.MinValue)
                    exp = (long)(c.Expires.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
                string domain = c.IsHttpOnly ? "#HttpOnly_" + c.Domain : c.Domain;
                string includeSub = c.Domain.StartsWith(".") ? "TRUE" : "FALSE";
                sb.AppendLine(domain + "\t" + includeSub + "\t" + c.Path + "\t" +
                    (c.IsSecure ? "TRUE" : "FALSE") + "\t" + exp + "\t" + c.Name + "\t" + c.Value);
            }
            File.WriteAllText(cookiesPath, sb.ToString());

            if (loggedIn)
            {
                MessageBox.Show(this,
                    "Facebook login saved (" + all.Count + " cookies).\r\n\r\nDownloads should now work.",
                    "Facebook login", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult r = MessageBox.Show(this,
                    "Cookies saved, but you do NOT seem to be logged in to Facebook.\r\n" +
                    "Videos that need a login will probably still fail.\r\n\r\n" +
                    "Close anyway?",
                    "Facebook login", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                    DialogResult = DialogResult.OK;
                else
                    btnSave.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save cookies:\r\n\r\n" + ex.Message,
                "Facebook login", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSave.Enabled = true;
        }
    }
}

// ============================================================================
//  Theme: light/dark Apple-style palette (follows the Windows setting,
//  override with --theme light|dark) plus drawing helpers.
// ============================================================================
static class Theme
{
    public static bool Dark;
    public static Color Bg, Card, Text, Sub, Sep, Accent, ControlBg;

    public static void Init(string[] args)
    {
        bool? forced = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--theme")
                forced = args[i + 1].Equals("dark", StringComparison.OrdinalIgnoreCase);

        if (forced.HasValue)
        {
            Dark = forced.Value;
        }
        else
        {
            Dark = false;
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    if (v is int) Dark = (int)v == 0;
                }
            }
            catch { }
        }

        if (Dark)
        {
            Bg        = Color.FromArgb(0x1C, 0x1C, 0x1E);
            Card      = Color.FromArgb(0x2C, 0x2C, 0x2E);
            Text      = Color.FromArgb(0xF2, 0xF2, 0xF7);
            Sub       = Color.FromArgb(0x98, 0x98, 0x9D);
            Sep       = Color.FromArgb(0x38, 0x38, 0x3A);
            Accent    = Color.FromArgb(0xBF, 0x5A, 0xF2); // Apple purple (dark)
            ControlBg = Color.FromArgb(0x3A, 0x3A, 0x3C);
        }
        else
        {
            Bg        = Color.FromArgb(0xF5, 0xF5, 0xF7);
            Card      = Color.White;
            Text      = Color.FromArgb(0x1D, 0x1D, 0x1F);
            Sub       = Color.FromArgb(0x6E, 0x6E, 0x73);
            Sep       = Color.FromArgb(0xD2, 0xD2, 0xD7);
            Accent    = Color.FromArgb(0xAF, 0x52, 0xDE); // Apple purple (light)
            ControlBg = Color.FromArgb(0xE8, 0xE8, 0xED);
        }
    }

    // Lightens (positive delta) or darkens (negative) a color per channel.
    public static Color Shift(Color c, int delta)
    {
        return Color.FromArgb(c.A,
            Clamp(c.R + delta), Clamp(c.G + delta), Clamp(c.B + delta));
    }

    static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

    public static GraphicsPath RoundedRect(Rectangle b, int radius)
    {
        int d = radius * 2;
        if (d > b.Width) d = b.Width;
        if (d > b.Height) d = b.Height;
        GraphicsPath p = new GraphicsPath();
        p.AddArc(b.X, b.Y, d, d, 180, 90);
        p.AddArc(b.Right - d, b.Y, d, d, 270, 90);
        p.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
        p.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Apply the Windows dark title bar for dark-theme forms.
    public static void ApplyTitleBarTheme(IntPtr hwnd)
    {
        try
        {
            int dark = Dark ? 1 : 0;
            // Attribute 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win11 / Win10 20H1+)
            // Attribute 19 = older build fallback (Win10 2004). Try both.
            DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
            DwmSetWindowAttribute(hwnd, 19, ref dark, 4);
        }
        catch { }
    }
}

// ============================================================================
//  Rounded button with hover/pressed/disabled states.
//  Accent = purple call-to-action, default = subtle gray.
// ============================================================================
class AppleButton : Button
{
    public bool Accent;
    bool hover, pressed;

    public AppleButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 9.75F);
        Cursor = Cursors.Hand;
        Size = new Size(100, 30);
        BackColor = Theme.Bg;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush bgBrush = new SolidBrush(Theme.Bg))
            g.FillRectangle(bgBrush, 0, 0, Width, Height);

        Color fill, text;
        if (!Enabled)
        {
            fill = Theme.ControlBg;
            text = Theme.Sub;
        }
        else if (Accent)
        {
            fill = Theme.Accent;
            if (pressed) fill = Theme.Shift(fill, -40);
            else if (hover) fill = Theme.Shift(fill, -18);
            text = Color.White;
        }
        else
        {
            fill = Theme.ControlBg;
            if (pressed) fill = Theme.Shift(fill, -30);
            else if (hover) fill = Theme.Shift(fill, -14);
            text = Theme.Text;
        }

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Theme.RoundedRect(r, 9))
        using (SolidBrush br = new SolidBrush(fill))
            g.FillPath(br, path);

        TextRenderer.DrawText(g, Text, Font, r, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }
}

// ============================================================================
//  Rounded text field: borderless TextBox inside a painted card,
//  with an accent focus ring.
// ============================================================================
class AppleTextBox : Panel
{
    public readonly TextBox Inner = new TextBox();
    bool focused;

    public AppleTextBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 30;
        Padding = new Padding(10, 6, 10, 6);
        Inner.BorderStyle = BorderStyle.None;
        Inner.BackColor = Theme.Card;
        Inner.ForeColor = Theme.Text;
        Inner.Dock = DockStyle.Fill;
        Inner.Enter += delegate { focused = true; Invalidate(); };
        Inner.Leave += delegate { focused = false; Invalidate(); };
        Controls.Add(Inner);
    }

    public override string Text
    {
        get { return Inner.Text; }
        set { Inner.Text = value; }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Inner.ForeColor = Enabled ? Theme.Text : Theme.Sub;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush bgBrush = new SolidBrush(Theme.Bg))
            g.FillRectangle(bgBrush, 0, 0, Width, Height);

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Theme.RoundedRect(r, 8))
        using (SolidBrush br = new SolidBrush(Theme.Card))
            g.FillPath(br, path);
        using (GraphicsPath path = Theme.RoundedRect(r, 8))
        using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Sep, focused ? 2F : 1F))
            g.DrawPath(pen, path);
    }
}

// ============================================================================
//  Flat combo box with rounded card, chevron, and themed dropdown items.
// ============================================================================
class AppleComboBox : ComboBox
{
    public AppleComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        Font = new Font("Segoe UI", 9.75F);
        ItemHeight = 24;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = selected ? Theme.Accent : Theme.Card;
        Color fg = selected ? Color.White : Theme.Text;
        using (SolidBrush br = new SolidBrush(bg))
            e.Graphics.FillRectangle(br, e.Bounds);
        TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), Font,
            new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height),
            fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    // The native ComboBox paints its own arrow button, so OnPaint is not
    // enough: repaint the whole face AFTER the native paint (WM_PAINT).
    protected override void WndProc(ref Message m)
    {
        const int WM_PAINT = 0x000F;
        base.WndProc(ref m);
        if (m.Msg != WM_PAINT || !IsHandleCreated) return;
        using (Graphics g = CreateGraphics())
            PaintFace(g);
    }

    void PaintFace(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush bgBrush = new SolidBrush(Theme.Bg))
            g.FillRectangle(bgBrush, 0, 0, Width, Height);

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Theme.RoundedRect(r, 8))
        using (SolidBrush br = new SolidBrush(Theme.Card))
            g.FillPath(br, path);
        using (GraphicsPath path = Theme.RoundedRect(r, 8))
        using (Pen pen = new Pen(Theme.Sep, 1F))
            g.DrawPath(pen, path);

        string text = SelectedIndex >= 0 ? Items[SelectedIndex].ToString() : "";
        TextRenderer.DrawText(g, text, Font, new Rectangle(10, 0, Width - 36, Height),
            Enabled ? Theme.Text : Theme.Sub,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        int cx = Width - 18, cy = Height / 2;
        using (Pen pen = new Pen(Theme.Sub, 1.6F))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, cx - 5, cy - 2, cx, cy + 3);
            g.DrawLine(pen, cx, cy + 3, cx + 5, cy - 2);
        }
    }
}

// ============================================================================
//  Thin rounded progress bar with accent fill.
// ============================================================================
class AppleProgressBar : Control
{
    int _value;
    public int Maximum;

    public AppleProgressBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 6;
        Maximum = 1000;
    }

    public int Value
    {
        get { return _value; }
        set
        {
            _value = value < 0 ? 0 : (value > Maximum ? Maximum : value);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush bgBrush = new SolidBrush(Theme.Bg))
            g.FillRectangle(bgBrush, 0, 0, Width, Height);

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = Theme.RoundedRect(r, 3))
        using (SolidBrush br = new SolidBrush(Theme.Sep))
            g.FillPath(br, path);

        if (_value > 0 && Maximum > 0)
        {
            int w = (int)((long)(Width - 1) * _value / Maximum);
            if (w < 6) w = 6;
            if (w > Width - 1) w = Width - 1;
            using (GraphicsPath path = Theme.RoundedRect(new Rectangle(0, 0, w, Height - 1), 3))
            using (SolidBrush br = new SolidBrush(Theme.Accent))
                g.FillPath(br, path);
        }
    }
}
