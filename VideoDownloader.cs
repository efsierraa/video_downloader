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
//     /out:VideoDownloader.exe VideoDownloader.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Net;

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

        // The app never downloads anything itself: every tool it needs ships
        // next to the exe in the release zip. (Only "yt-dlp -U" downloads,
        // and that is yt-dlp updating itself.)
        string missing = MissingFiles("yt-dlp.exe", "deno.exe", "ffmpeg.exe", "ffprobe.exe");
        if (missing != null)
        {
            WriteColor(ConsoleColor.Red,
                "Some required files are missing from this folder: " + missing);
            Console.WriteLine();
            Console.WriteLine("Re-download the complete package from:");
            Console.WriteLine("  https://github.com/efsierraa/video_downloader/releases");
            Console.WriteLine("Or run Get-Video.ps1 once -- it downloads the missing tools for you.");
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
