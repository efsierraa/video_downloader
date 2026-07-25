// VideoDownloader.exe -- double-click version of Get-Video.ps1
// Downloads videos from Facebook and YouTube using yt-dlp.
//
// Usage:
//   VideoDownloader.exe                      (asks for the URL)
//   VideoDownloader.exe <url> [-o outdir]    (no prompt)
//   VideoDownloader.exe -u                   (update yt-dlp)
//
// Compile (no Visual Studio needed):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe ^
//     /out:VideoDownloader.exe /r:System.IO.Compression.dll ^
//     /r:System.IO.Compression.FileSystem.dll VideoDownloader.cs

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;

class VideoDownloader
{
    static readonly string ExeDir = Path.GetDirectoryName(
        System.Reflection.Assembly.GetExecutingAssembly().Location);

    static readonly string YtDlp   = Path.Combine(ExeDir, "yt-dlp.exe");
    static readonly string Deno    = Path.Combine(ExeDir, "deno.exe");
    static readonly string Ffmpeg  = Path.Combine(ExeDir, "ffmpeg.exe");
    static readonly string Ffprobe = Path.Combine(ExeDir, "ffprobe.exe");

    const string Format = "bv*+ba/b";

    static int Main(string[] args)
    {
        Console.Title = "Video Downloader";
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        string url = null;
        string outputDir = Path.Combine(ExeDir, "downloads");
        bool update = false;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "-u" || a == "--update" || a == "-Update" || a == "/u")
                update = true;
            else if ((a == "-o" || a == "--output") && i + 1 < args.Length)
                outputDir = args[++i];
            else if (url == null)
                url = a;
        }

        try
        {
            InstallMissingTools();
        }
        catch (Exception ex)
        {
            WriteColor(ConsoleColor.Red,
                "\nCould not download the required tools. Check your internet connection and try again.");
            WriteColor(ConsoleColor.Red, ex.Message);
            PauseIfInteractive(args);
            return 1;
        }

        if (update)
        {
            int rc = RunProcess(YtDlp, "-U");
            PauseIfInteractive(args);
            return rc;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            Console.Write("Paste the video URL (Facebook or YouTube): ");
            url = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            WriteColor(ConsoleColor.Red, "No URL given.");
            PauseIfInteractive(args);
            return 1;
        }

        url = url.Trim().Trim('"');

        Directory.CreateDirectory(outputDir);
        string outTemplate = Path.Combine(outputDir, "%(title)s.%(ext)s");

        string yargs = "";
        if (File.Exists(Deno))
            yargs += "--js-runtimes \"deno:" + Deno + "\" ";
        if (File.Exists(Ffmpeg))
            yargs += "--ffmpeg-location \"" + ExeDir + "\" ";
        string cookies = Path.Combine(ExeDir, "cookies.txt");
        if (File.Exists(cookies))
            yargs += "--cookies \"" + cookies + "\" ";
        yargs += "--format \"" + Format + "\" ";
        yargs += "--output \"" + outTemplate + "\" ";
        yargs += "\"" + url + "\"";

        WriteColor(ConsoleColor.Cyan, "Downloading: " + url);
        WriteColor(ConsoleColor.Cyan, "Saving to:   " + outputDir);
        Console.WriteLine();

        int code = RunProcess(YtDlp, yargs);

        Console.WriteLine();
        if (code == 0)
            WriteColor(ConsoleColor.Green, "Download completed successfully.");
        else
            WriteColor(ConsoleColor.Red, "Download failed (exit code " + code + ").");

        PauseIfInteractive(args);
        return code;
    }

    // Downloads yt-dlp.exe, deno.exe, ffmpeg.exe and ffprobe.exe into the
    // program folder if any of them are missing (same as Get-Video.ps1).
    static void InstallMissingTools()
    {
        bool needYtDlp  = !File.Exists(YtDlp);
        bool needDeno   = !File.Exists(Deno);
        bool needFfmpeg = !File.Exists(Ffmpeg) || !File.Exists(Ffprobe);

        if (!needYtDlp && !needDeno && !needFfmpeg) return;

        WriteColor(ConsoleColor.Yellow,
            "Some required tools are missing. Downloading them now (one-time setup)...");

        string tmp = Path.Combine(Path.GetTempPath(),
            "getvideo-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);

        try
        {
            if (needYtDlp)
            {
                WriteColor(ConsoleColor.DarkGray, "  -> yt-dlp.exe");
                DownloadFile(
                    "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
                    YtDlp, "yt-dlp.exe");
            }

            if (needDeno)
            {
                WriteColor(ConsoleColor.DarkGray, "  -> deno.exe");
                string zip = Path.Combine(tmp, "deno.zip");
                DownloadFile(
                    "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip",
                    zip, "deno.zip");
                ExtractEntry(zip, "deno.exe", Deno);
            }

            if (needFfmpeg)
            {
                WriteColor(ConsoleColor.DarkGray,
                    "  -> ffmpeg.exe + ffprobe.exe (large download, please wait)");
                string zip = Path.Combine(tmp, "ffmpeg.zip");
                DownloadFile(
                    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
                    zip, "ffmpeg.zip");
                ExtractEntry(zip, "ffmpeg.exe", Ffmpeg);
                ExtractEntry(zip, "ffprobe.exe", Ffprobe);
            }

            WriteColor(ConsoleColor.Green, "Setup complete.");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    static void DownloadFile(string url, string destPath, string label)
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
                    Console.Write("\r    {0} {1}%  ", label, e.ProgressPercentage);
                }
            };
            wc.DownloadFileCompleted += delegate(object s, AsyncCompletedEventArgs e)
            {
                failure = e.Error;
                done.Set();
            };

            wc.DownloadFileAsync(new Uri(url), destPath);
            done.Wait();

            Console.Write("\r                                        \r");
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

    static int RunProcess(string exe, string arguments)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = exe;
        psi.Arguments = arguments;
        psi.UseShellExecute = false;
        Process p = Process.Start(psi);
        p.WaitForExit();
        return p.ExitCode;
    }

    static void WriteColor(ConsoleColor color, string message)
    {
        ConsoleColor old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = old;
    }

    // Keep the window open after a double-click so the result can be read.
    static void PauseIfInteractive(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine();
            Console.Write("Press Enter to close this window...");
            Console.ReadLine();
        }
    }
}
