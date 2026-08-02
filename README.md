# 🎬 Video Downloader

> Download videos from **Facebook** and **YouTube** — no install, no setup.  
> Everything is portable and lives right in this folder.

---

Made with ❤️ for **Mariana**

---

## ✨ Modern interface

The windowed app has a clean Apple-inspired interface with:

- Light and dark themes that follow Windows automatically
- Apple purple accents, rounded buttons, and rounded input fields
- A responsive layout that adapts when the window is resized
- A slim progress bar with clear download status
- A collapsible **Show details** panel for technical logs
- A **Dark mode** / **Light mode** button to switch themes instantly
- A matching dark title bar when Windows supports it

To override the Windows theme, run one of these commands from PowerShell:

```powershell
.\Video Downloader.exe --theme light
.\Video Downloader.exe --theme dark
```

The theme option can also be combined with automatic download arguments.

---

## 🖥️ Easiest way — the windowed app

Double-click **Video Downloader.exe**, paste a URL, and click Download.

1. Open **Video Downloader.exe**
2. Paste the video URL into the top box (Ctrl+V works)
3. Click the big **Download** button and wait for the progress bar
4. The video lands in the `downloads` folder — the app offers to open it for you

**Extra controls:**

| Button / Option | What it does |
|---|---|
| **Browse…** | Pick where to save the video (folder + file name) |
| **Quality** list | Choose 1080p / 720p / 480p, or **Audio only (MP3)** |
| **Update yt-dlp** | Refresh the download engine if sites ever stop working |
| **Facebook login…** | Log into Facebook so the app can download private videos |

The window is resizable and automatically matches your Windows light/dark theme.
Use the **Dark mode** / **Light mode** button in the footer to switch themes
without restarting the app.
Click **Show details ▾** beneath the progress area whenever you want to see
the technical download log.

> Everything the app needs is already in the zip — the app itself downloads nothing  
> (which also keeps antivirus programs happy). See [the helper tools](#-helper-tools-already-included).

---

## ⚡ Alternative — the console app

For a quick, no-frills download:

1. Double-click **VideoDownloader.exe** (the one with the black icon)
2. Paste the URL (right-click → paste) and press Enter
3. The video is saved in `downloads`

To update the engine from the console:

```
.\VideoDownloader.exe -u
```

---

## 📜 PowerShell script

The script version does everything the apps do, but as a readable text file you can inspect.

### One-time setup

Open **PowerShell** inside this folder:

- **Windows 10:** Shift + right-click on empty space → *Open PowerShell window here*
- **Windows 11:** Shift + right-click on empty space → *Open in Terminal*

Then run this once (type **Y** when asked):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

### Download a video

```powershell
.\Get-Video.ps1 "https://www.youtube.com/watch?v=jNQXAC9IVRw"
```

Or run without a URL to be prompted:

```powershell
.\Get-Video.ps1
```

### Save somewhere else

```powershell
.\Get-Video.ps1 "URL" -OutputDir "D:\MyVideos"
```

### Update the engine

```powershell
.\Get-Video.ps1 -Update
```

---

## 🔑 Facebook login (for videos that need it)

Most Facebook videos need you to be logged in. The windowed app handles this in one click:

1. Open **Video Downloader.exe**
2. Click **Facebook login…** at the bottom
3. A real Facebook window opens — log in with your account
4. Click **Save cookies & Close**
5. Try the download again

The button changes to **Facebook login (saved)** when it's ready.

Your login lives only in this folder (`cookies.txt` + `webview2-data\`) and is never shared.  
⚠️ **Never share `cookies.txt`** — it's like a password.

> The console app and the PowerShell script also use `cookies.txt` automatically if it exists.

### No app? Create cookies.txt by hand

If your antivirus blocks the exe completely:

1. In your browser, install the **"Get cookies.txt LOCALLY"** extension
2. Go to **www.facebook.com** and log in
3. Click the extension icon → export cookies
4. Save the file as **cookies.txt** in this folder

The script and the apps pick it up on the next download.

---

## 🧰 Helper tools (already included)

Everything the app needs comes **already inside the zip** — the app itself downloads nothing. Here's what each helper does:

| File | Why |
|---|---|
| **yt-dlp.exe** | The actual video downloader engine ([github.com/yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp)) |
| **deno.exe** | JavaScript runtime — YouTube scrambles URLs with JS, and yt-dlp uses this to unscramble them |
| **ffmpeg.exe + ffprobe.exe** | Merges video + audio streams into one file; also converts to MP3 |
| **WebView2 DLLs** (3 files) | A tiny Microsoft browser for the Facebook login window (GUI app only) |

The only things that ever get downloaded are the videos themselves and, when you click **Update yt-dlp**, yt-dlp's own self-update. Nothing is installed system-wide.

> If a helper file ever gets deleted, re-download the zip — or run `Get-Video.ps1` once, it can fetch missing tools for you.

---

## ⚠️ Antivirus false positive

Windows Defender may flag the `.exe` files with names like **Trojan:Win32/Wacatac.B!ml** or **Trojan:Win32/Sabsik.FL.A!ml**.

**This is a false positive** — the `!ml` suffix means it's a machine-learning *guess*, not an actual virus. It happens because the app is **not digitally signed**: a signing certificate costs money every year, and this is a free tool, so it is not signed for economical reasons. Unsigned apps with few downloads get guessed as malware "just in case".

> ⚠️ **Important:** VirusTotal runs Microsoft's engine at *maximum* sensitivity (cloud-delivered, aggressive ML). A file flagged there may run perfectly fine on your own PC with normal Windows Defender settings. Check your actual computer — it likely doesn't block it.

(The app itself downloads nothing — all helper tools come inside the zip.)

### The fix

**Report the false positive to Microsoft** (free, they'll whitelist this build):

> https://www.microsoft.com/en-us/wdsi/filesubmission

You can also add this folder to your antivirus exclusions:

> Windows Security → Virus & threat protection → Manage settings → **Exclusions** → Add this folder

### Don't trust an .exe? Use the script instead

`Get-Video.ps1` is a plain text file — open it in Notepad and read every line. It does exactly the same job.

You can also verify the `.exe` files at [virustotal.com](https://www.virustotal.com) — the vast majority of engines report them as clean.

---

## 📝 Notes

- If a helper file gets deleted, re-download the zip (or run `Get-Video.ps1` once — it fetches missing tools)
- Long videos may take a few minutes
- Private/unlisted Facebook videos may not work
- Age-restricted YouTube videos may need a login (try a different one)

---

## 📄 License

MIT — see [LICENSE](LICENSE) for the full text.
