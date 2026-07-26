======================================================================
                VIDEO DOWNLOADER (Facebook & YouTube)
======================================================================

This folder contains a tool that downloads videos from Facebook and
YouTube. You don't need to install anything -- everything is already
in this folder and ready to use.


----------------------------------------------------------------------
EASIEST WAY: "Video Downloader.exe" (the app with a window)
----------------------------------------------------------------------

1. Double-click  Video Downloader.exe  in this folder.
2. Paste the video URL into the top box (Ctrl+V works).
3. Click the big  Download  button and wait for the bar to fill.
4. The video is saved in the  downloads  folder inside this folder.
   When it finishes, the app offers to open that folder for you.

Optional:
   - Click  Browse...  to save the video somewhere else. A
     "Save video as..." window opens where you can pick both the
     folder and the file name.
   - Use the  Quality  list to pick a smaller size (1080p, 720p,
     480p) or  Audio only (MP3)  to keep just the sound.
   - If downloads ever stop working, click  Update yt-dlp.

The very first time, the app downloads its helper programs by
itself (yt-dlp, deno, ffmpeg, and the WebView2 browser components
used for Facebook login). This takes a few minutes and needs
internet access. It only happens once.


----------------------------------------------------------------------
ALTERNATIVE: VideoDownloader.exe (black console window)
----------------------------------------------------------------------

1. Double-click  VideoDownloader.exe  in this folder.
2. When it asks, paste the video URL (right-click inside the black
   window to paste) and press Enter.
3. Wait for the download to finish. The video is saved in the
   downloads  folder inside this folder.

To update the downloader engine, run this in PowerShell:

    .\VideoDownloader.exe -u


----------------------------------------------------------------------
ANTIVIRUS WARNING (FALSE POSITIVE)
----------------------------------------------------------------------

   Windows Defender (or another antivirus) may flag the .exe files
   with a name like "Trojan:Win32/Sabsik.FL.A!ml". This is a FALSE
   POSITIVE: the detection is a machine-learning guess, not an
   actual virus.

   Why it happens: the app is NOT digitally signed. A code-signing
   certificate costs money every year, and this is a free tool, so
   it is not signed for economical reasons. Unsigned apps that
   download and run helper programs (like this one does -- see the
   next section) look suspicious to antivirus software, so they
   sometimes block them "just in case".

   THE SOLUTION: tell your antivirus to trust the app. In Windows
   Security, go to:
       Virus & threat protection > Manage settings >
       Exclusions > Add an exclusion > Folder
   and pick this folder. Then unzip/copy the app again.

   If you prefer not to trust an .exe at all, you can ALWAYS use
   the script version instead: Get-Video.ps1 (instructions below).
   It is a plain text file -- open it in Notepad and you can read
   exactly what it does. It does the same job as the .exe files.

   You can also upload the .exe files to www.virustotal.com --
   the large majority of antivirus engines report them as clean.


----------------------------------------------------------------------
WHAT GETS DOWNLOADED (AND WHY)
----------------------------------------------------------------------

   The first time you run the app or the script, it downloads a
   few helper programs into this folder. This is normal and only
   happens once. Here is what they are and why they are needed:

   - yt-dlp.exe   The actual video downloader. This tool is just
                  a friendly front-end around yt-dlp, a well-known
                  open-source project (github.com/yt-dlp/yt-dlp).

   - deno.exe     A JavaScript runtime. YouTube "scrambles" its
                  video URLs with JavaScript, and yt-dlp uses deno
                  to unscramble them. Without it, YouTube
                  downloads fail.

   - ffmpeg.exe + ffprobe.exe
                  Video/audio tools. Most videos download as
                  separate video and audio streams, and ffmpeg
                  merges them into one file. It also converts the
                  audio to MP3 when you pick "Audio only".

   - WebView2 components (3 .dll files, windowed app only)
                  A small Microsoft browser used by the "Facebook
                  login..." window, so you can log in to Facebook
                  and download videos that need a login. On old
                  Windows (.NET Framework before 4.7.2), the app
                  also downloads .NET Standard support files
                  (netstandard.dll and friends) that the login
                  window needs.

   Everything is downloaded from the official download locations
   of each project (github.com, nuget.org, gyan.dev). Nothing
   else is installed and everything stays inside this folder.


----------------------------------------------------------------------
HOW TO START (one-time setup)
----------------------------------------------------------------------

Everything below describes the PowerShell version (Get-Video.ps1),
which does the same thing and offers more options.

Step 1:  Open this folder in File Explorer (the yellow folder icon).
         You should see the files: Get-Video.ps1, yt-dlp.exe, etc.

Step 2:  Open PowerShell inside this folder. Pick EITHER method:

         METHOD A (easiest):
           - Hold the Shift key on your keyboard
           - While holding Shift, right-click on an EMPTY white space
             inside the folder (not on any file)
           - In the menu, click:
               "Open PowerShell window here"   (Windows 10), or
               "Open in Terminal"              (Windows 11)

         METHOD B (address bar trick):
           - At the top of the File Explorer window there is an
             address bar that shows where you are, like:
                 > Documents > video_downloader
           - Click ONCE on that bar. It turns into a text box and
             the path gets highlighted (selected in blue):
                 C:\Users\maria\Documents\video_downloader
           - While it is highlighted, type the word:
                 powershell
             (your typing replaces the highlighted text)
           - Press Enter.

         Either way, a blue (or black) window opens with white text.
         This is PowerShell, and it is already "inside" this folder.

Step 3:  In the PowerShell window, copy and paste this line, then
         press Enter:

             Set-ExecutionPolicy -Scope CurrentUser RemoteSigned

         (How to paste: right-click anywhere in the blue window.)

         It will ask you to confirm. Type  Y  and press Enter.

         You only need to do Step 3 once, the very first time.
         From now on, whenever you want to download a video, just
         do Step 2 and then follow the instructions below.


----------------------------------------------------------------------
HOW TO DOWNLOAD A VIDEO
----------------------------------------------------------------------

1. Go to Facebook or YouTube in your web browser.
2. Copy the video URL from the address bar. For example:

      -- Facebook:  https://www.facebook.com/watch/?v=1306600044919943
      -- YouTube:   https://www.youtube.com/watch?v=jNQXAC9IVRw

3. Open this folder in PowerShell (same as Step 2 above).
4. In the blue PowerShell window, type the following and press Enter:

      .\Get-Video.ps1

   (The backslash is important -- type it exactly as shown.)

5. When it asks for the video URL, right-click to paste the URL you
   copied in Step 2, then press Enter.

6. Wait for the download to finish. You'll see a progress bar.

7. The downloaded video will be in the  downloads  folder inside
   this folder. Open it and play it like any other video file.


----------------------------------------------------------------------
QUICK ONE-LINE DOWNLOAD (skip the prompt)
----------------------------------------------------------------------

   Instead of steps 4-5 above, you can paste the URL directly on
   the same line. For example:

      .\Get-Video.ps1 "https://www.youtube.com/watch?v=jNQXAC9IVRw"

   The quotes around the URL are important -- don't forget them.

   In PowerShell, you paste by right-clicking in the window.


----------------------------------------------------------------------
CUSTOM LOCATION (save videos somewhere else)
----------------------------------------------------------------------

   By default, videos are saved in the  downloads  folder inside
   this folder. To save them somewhere else, add -OutputDir:

      .\Get-Video.ps1 "https://www.youtube.com/watch?v=jNQXAC9IVRw" -OutputDir "D:\MyVideos"

   The folder you specify must already exist.


----------------------------------------------------------------------
KEEPING THE TOOL UPDATED
----------------------------------------------------------------------

   If downloads ever stop working (websites change over time), run:

      .\Get-Video.ps1 -Update

   This updates the downloader engine to the latest version.
   It takes a few seconds and requires internet access.


----------------------------------------------------------------------
FACEBOOK VIDEOS THAT FAIL WITH "Cannot parse data"
----------------------------------------------------------------------

   Most Facebook videos need you to be logged in. The app can log in
   for you (one-time, about 1 minute):

    1. Open  Video Downloader.exe.
    2. Click the  Facebook login...  button at the bottom.
    3. A window with the real Facebook website opens. Log in there
       with your usual Facebook email and password.
    4. Once you are logged in, click  Save cookies & Close.
    5. Try the download again -- it will now work.

    Once your login is saved, the button changes to
    "Facebook login (saved)" so you know it is ready.

   Your login stays inside this folder (the  webview2-data  folder
   and the  cookies.txt  file) and is only used to download videos.
   If Facebook downloads fail again after a long time, repeat the
   steps above (your login expired).

   Never share  cookies.txt  with anyone -- it is like a password.

    (The PowerShell script and the console VideoDownloader.exe also
    use  cookies.txt  automatically once it exists.)

    NO APP? If you cannot run  Video Downloader.exe  at all (for
    example, your antivirus blocks it), you can create  cookies.txt
    by hand:

     1. In your normal browser (Chrome, Edge, Firefox), install the
        extension "Get cookies.txt LOCALLY".
     2. Go to  www.facebook.com  and log in as usual.
     3. Click the extension icon and export the cookies.
     4. Save the exported file as  cookies.txt  inside this folder
        (replace the old one if asked).

    The script and the apps pick it up automatically on the next
    download.


----------------------------------------------------------------------
NOTES
----------------------------------------------------------------------

   - If the helper programs (yt-dlp.exe, deno.exe, ffmpeg.exe,
     ffprobe.exe) are ever deleted, the script downloads them again
     automatically the next time you run it.
   - Downloads may take a few minutes for long videos.
   - Facebook private/unlisted videos may not work.
   - If a YouTube video is age-restricted, you may need to log in
     (not covered here -- just try a different video).
