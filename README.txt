======================================================================
                VIDEO DOWNLOADER (Facebook & YouTube)
======================================================================

This folder contains a tool that downloads videos from Facebook and
YouTube. You don't need to install anything -- everything is already
in this folder and ready to use.


----------------------------------------------------------------------
HOW TO START (one-time setup)
----------------------------------------------------------------------

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
NOTES
----------------------------------------------------------------------

   - Downloads may take a few minutes for long videos.
   - Facebook private/unlisted videos may not work.
   - If a YouTube video is age-restricted, you may need to log in
     (not covered here -- just try a different video).
