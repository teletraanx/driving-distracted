# Setup Instructions:

## FFmpeg Setup
IMPORTANT: Administrator access is required

1. Download and install 7zip
- [https://www.7-zip.org/download.html]
- Select and download the "64-bit Windows x64" installer
- Run it and follow the installation instruction

2. Download and install FFmpeg
- [https://www.gyan.dev/ffmpeg/builds/]
- Select and download "ffmpeg-release-full.7z" under the "release builds" section
- Extract it using 7zip (right-click and choose an "Extract Here" from the "7zip" sub-menu)
- Rename the resulting folder ("ffmpeg-8.0-full_build") to "FFmpeg"
- Drag and drop the "FFmpeg" folder to "Local Disk (C:)" found in "This PC"
- Type "cmd" in the Windows search bar (in the Start Menu or on the task bar), right-click "Command Prompt", select "Run as administrator", and then click "Yes".
- Copy and paste `setx /m PATH "C:\ffmpeg\bin;%PATH%"` into the Command Prompt, then press Enter
- FFmpeg should now be installed. To test, reopen the Command Prompt and type `ffmpeg -version`.

## Microphone Setup

Unity applications always listen to the default microphone as determined by Windows system settings. Refer to the following article for instructions on how to ensure that your microphone is working correctly: (https://www.wikihow.com/Change-the-Default-Microphone-in-Windows-11)

## Disclaimer
Windows 11 has been observed by many users to exhibit greater instability and inconsistency compared to Windows 10, particularly with regard to device management and audio subsystem behavior. As a result, audio input handling, system-level device selection, and related configuration steps may not function reliably across all Windows 11 installations or hardware configurations.

Because Unity applications depend on the operating system’s default audio input device and do not currently support switching microphones at runtime, proper behavior cannot be guaranteed on Windows 11. Users may encounter issues such as incorrect device selection, delayed device enumeration, inconsistent microphone detection, or other OS-level irregularities beyond the control of the application.

For these reasons, Unity-based applications running on Windows 11 are not guaranteed to work as expected, even when all recommended setup steps have been followed. Users should be aware of this limitation, and results may vary depending on system stability, OS version, hardware, and driver support.
