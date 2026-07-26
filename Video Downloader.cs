// Video Downloader.exe -- simple Windows app for downloading videos
// from Facebook and YouTube (graphical front-end for yt-dlp).
//
// Double-click it, paste the video URL, click Download.
//
// Command line (optional, used for unattended downloads and testing):
//   "Video Downloader.exe" --url <url> --out <dir> --quality best|1080p|720p|480p|mp3 --auto
//
// Compile (no Visual Studio needed):
//   .\Build-App.ps1
// or manually:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
//     /out:"Video Downloader.exe" /r:System.dll /r:System.Drawing.dll ^
//     /r:System.Windows.Forms.dll /r:System.IO.Compression.dll ^
//     /r:System.IO.Compression.FileSystem.dll "Video Downloader.cs"

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
    TextBox txtUrl;
    TextBox txtOut;
    ComboBox cmbQuality;
    Button btnBrowse;
    Button btnDownload;
    Button btnUpdate;
    Button btnFbLogin;
    ProgressBar progress;
    Label lblStatus;
    Label lblVersion;
    TextBox txtLog;

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
    //  UI construction
    // ========================================================================
    void BuildUi()
    {
        Font = new Font("Segoe UI", 9F);
        Text = "Video Downloader";
        ClientSize = new Size(520, 424);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        Label lblUrl = new Label();
        lblUrl.Text = "Video URL:";
        lblUrl.AutoSize = true;
        lblUrl.Location = new Point(12, 16);
        Controls.Add(lblUrl);

        txtUrl = new TextBox();
        txtUrl.Location = new Point(90, 13);
        txtUrl.Size = new Size(418, 23);
        Controls.Add(txtUrl);

        Label lblSave = new Label();
        lblSave.Text = "Save to:";
        lblSave.AutoSize = true;
        lblSave.Location = new Point(12, 49);
        Controls.Add(lblSave);

        txtOut = new TextBox();
        txtOut.Location = new Point(90, 46);
        txtOut.Size = new Size(322, 23);
        txtOut.Text = defaultOutDir;
        Controls.Add(txtOut);

        btnBrowse = new Button();
        btnBrowse.Text = "Browse...";
        btnBrowse.Location = new Point(418, 45);
        btnBrowse.Size = new Size(90, 25);
        btnBrowse.Click += btnBrowse_Click;
        Controls.Add(btnBrowse);

        Label lblQuality = new Label();
        lblQuality.Text = "Quality:";
        lblQuality.AutoSize = true;
        lblQuality.Location = new Point(12, 82);
        Controls.Add(lblQuality);

        cmbQuality = new ComboBox();
        cmbQuality.Location = new Point(90, 79);
        cmbQuality.Size = new Size(180, 23);
        cmbQuality.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbQuality.Items.AddRange(QualityLabels);
        cmbQuality.SelectedIndex = 0;
        Controls.Add(cmbQuality);

        btnDownload = new Button();
        btnDownload.Text = "Download";
        btnDownload.Font = new Font(Font, FontStyle.Bold);
        btnDownload.Location = new Point(12, 114);
        btnDownload.Size = new Size(496, 42);
        btnDownload.Click += btnDownload_Click;
        Controls.Add(btnDownload);
        AcceptButton = btnDownload;

        progress = new ProgressBar();
        progress.Location = new Point(12, 168);
        progress.Size = new Size(496, 20);
        progress.Minimum = 0;
        progress.Maximum = 1000;
        Controls.Add(progress);

        lblStatus = new Label();
        lblStatus.Location = new Point(12, 196);
        lblStatus.Size = new Size(496, 20);
        lblStatus.AutoEllipsis = true;
        lblStatus.Text = "";
        Controls.Add(lblStatus);

        txtLog = new TextBox();
        txtLog.Location = new Point(12, 222);
        txtLog.Size = new Size(496, 150);
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Font = new Font("Consolas", 8.25F);
        txtLog.BackColor = Color.White;
        Controls.Add(txtLog);

        lblVersion = new Label();
        lblVersion.Location = new Point(12, 388);
        lblVersion.AutoSize = true;
        lblVersion.ForeColor = Color.Gray;
        lblVersion.Text = "yt-dlp ...";
        Controls.Add(lblVersion);

        btnUpdate = new Button();
        btnUpdate.Text = "Update yt-dlp";
        btnUpdate.Location = new Point(396, 382);
        btnUpdate.Size = new Size(112, 28);
        btnUpdate.Click += btnUpdate_Click;
        Controls.Add(btnUpdate);

        btnFbLogin = new Button();
        btnFbLogin.Location = new Point(238, 382);
        btnFbLogin.Size = new Size(150, 28);
        btnFbLogin.Click += btnFbLogin_Click;
        Controls.Add(btnFbLogin);
        UpdateFbLoginButton();
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
        SetStatus("Checking required tools...");

        RunBackground(delegate
        {
            EnsureTools();
            EnsureWebView2();

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
        try
        {
            using (FacebookLoginForm f = new FacebookLoginForm(cookies, userData))
                f.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not open the login window:\r\n\r\n" + ex.Message +
                "\r\n\r\n(Microsoft WebView2 Runtime is required. If Windows is old,\r\n" +
                "installing the free .NET Framework 4.8 update also fixes this.)",
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

    // ========================================================================
    //  First-time setup: download yt-dlp / deno / ffmpeg if missing
    // ========================================================================
    void EnsureTools()
    {
        bool needYtDlp  = !File.Exists(YtDlp);
        bool needDeno   = !File.Exists(Deno);
        bool needFfmpeg = !File.Exists(Ffmpeg) || !File.Exists(Ffprobe);

        if (!needYtDlp && !needDeno && !needFfmpeg) return;

        SetStatus("First-time setup: downloading required tools...");
        Log("Some required tools are missing. Downloading them now (one-time setup)...");

        string tmp = Path.Combine(Path.GetTempPath(),
            "getvideo-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);

        try
        {
            if (needYtDlp)
            {
                Log("  -> yt-dlp.exe");
                DownloadFile(
                    "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
                    YtDlp, "yt-dlp.exe");
            }

            if (needDeno)
            {
                Log("  -> deno.exe");
                string zip = Path.Combine(tmp, "deno.zip");
                DownloadFile(
                    "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip",
                    zip, "deno.zip");
                ExtractEntry(zip, "deno.exe", Deno);
            }

            if (needFfmpeg)
            {
                Log("  -> ffmpeg.exe + ffprobe.exe (large download, please wait)");
                string zip = Path.Combine(tmp, "ffmpeg.zip");
                DownloadFile(
                    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
                    zip, "ffmpeg.zip");
                ExtractEntry(zip, "ffmpeg.exe", Ffmpeg);
                ExtractEntry(zip, "ffprobe.exe", Ffprobe);
            }

            Log("Setup complete.");
            SetStatus("Setup complete.");
            SetProgress(0);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    void EnsureWebView2()
    {
        string wv2Core     = Path.Combine(ExeDir, "Microsoft.Web.WebView2.Core.dll");
        string wv2WinForms = Path.Combine(ExeDir, "Microsoft.Web.WebView2.WinForms.dll");
        string wv2Loader   = Path.Combine(ExeDir, "WebView2Loader.dll");

        bool needWv2 = !File.Exists(wv2Core) || !File.Exists(wv2WinForms) ||
                       !File.Exists(wv2Loader);
        bool needFacades = NeedsNetStandardFacades() &&
            !File.Exists(Path.Combine(ExeDir, "netstandard.dll"));

        if (!needWv2 && !needFacades) return;

        SetStatus("Downloading WebView2 components (one-time setup)...");
        Log("WebView2 components missing. Downloading now...");

        string tmp = Path.Combine(Path.GetTempPath(),
            "wv2setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);

        try
        {
            if (needWv2)
            {
                string zip = Path.Combine(tmp, "webview2.nupkg");
                DownloadFile(
                    "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2",
                    zip, "WebView2 SDK");

                using (ZipArchive archive = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string name = entry.Name;
                        if (string.Equals(name, "Microsoft.Web.WebView2.Core.dll",
                                StringComparison.OrdinalIgnoreCase))
                            entry.ExtractToFile(wv2Core, true);
                        else if (string.Equals(name, "Microsoft.Web.WebView2.WinForms.dll",
                                StringComparison.OrdinalIgnoreCase))
                            entry.ExtractToFile(wv2WinForms, true);
                        else if (string.Equals(name, "WebView2Loader.dll",
                                StringComparison.OrdinalIgnoreCase))
                            entry.ExtractToFile(wv2Loader, true);
                    }
                }
            }

            if (needFacades)
            {
                // The WebView2 .NET assemblies are .NET Standard 2.0 libraries.
                // .NET Framework 4.7.2+ supports that out of the box; older
                // frameworks need the facade assemblies next to the app,
                // otherwise the login window fails with errors like
                // "Could not load file or assembly System.ComponentModel.Primitives".
                Log("Older .NET Framework detected. Downloading .NET Standard support files...");
                SetStatus("Downloading .NET Framework support files...");
                string zip = Path.Combine(tmp, "netstandard.nupkg");
                DownloadFile(
                    "https://www.nuget.org/api/v2/package/NETStandard.Library/2.0.3",
                    zip, ".NET Standard support");

                using (ZipArchive archive = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith("build/netstandard2.0/ref/",
                                StringComparison.OrdinalIgnoreCase) &&
                            entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.ExtractToFile(Path.Combine(ExeDir, entry.Name), true);
                        }
                    }
                }
            }

            Log("WebView2 components ready.");
            SetStatus("WebView2 components ready.");
            SetProgress(0);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    // True if Windows has .NET Framework older than 4.7.2 (Release 461808),
    // which does not include .NET Standard 2.0 support built in.
    static bool NeedsNetStandardFacades()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                if (key != null)
                {
                    object rel = key.GetValue("Release");
                    if (rel is int && (int)rel >= 461808) return false;
                }
            }
        }
        catch { }
        return true;
    }

    void DownloadFile(string url, string destPath, string label)
    {
        using (WebClient wc = new WebClient())
        using (ManualResetEventSlim done = new ManualResetEventSlim(false))
        {
            Exception failure = null;
            int lastPct = -1;

            wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
            {
                if (e.ProgressPercentage != lastPct)
                {
                    lastPct = e.ProgressPercentage;
                    SetProgress(e.ProgressPercentage);
                    SetStatus("Downloading " + label + "... " + e.ProgressPercentage + "%");
                }
            };
            wc.DownloadFileCompleted += delegate(object s, AsyncCompletedEventArgs e)
            {
                failure = e.Error;
                done.Set();
            };

            wc.DownloadFileAsync(new Uri(url), destPath);
            done.Wait();

            if (failure != null) throw failure;
        }
    }

    static void ExtractEntry(string zipPath, string entryName, string destPath)
    {
        using (ZipArchive zip = ZipFile.OpenRead(zipPath))
        {
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(destPath, true);
                    return;
                }
            }
        }
        throw new FileNotFoundException(
            entryName + " not found inside " + Path.GetFileName(zipPath));
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
    Button btnSave;
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
        Controls.Add(bottom);

        lblHint = new Label();
        lblHint.AutoSize = false;
        lblHint.Dock = DockStyle.Fill;
        lblHint.TextAlign = ContentAlignment.MiddleLeft;
        lblHint.Padding = new Padding(8, 0, 0, 0);
        lblHint.Text = "Loading Facebook...";
        bottom.Controls.Add(lblHint);

        btnSave = new Button();
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
