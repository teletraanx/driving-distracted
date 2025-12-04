using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class MicrophoneManager : MonoBehaviour
{
    // Audio streaming variables
    private AudioClip audioClip;
    private byte[] audioBuffer;
    private NamedPipeServerStream pipeServer;
    private Process ffmpegProcess;
    private CancellationTokenSource cancellationTokenSource;
    private bool isStreaming = false;
    private string currentPipeName = "";

    // Shared state objects and variables
    private readonly object _lockObject = new();
    private string _currentOutput = "";
    
    // Events for game logic communication
    public event Action<int> OnNumberRecognized;  // Recognized number (1 or 2)

    private string ffmpegLinuxArguments = $"-f pulse -ar 48000 -ac 2 -i pipe:0 -vn -af \"whisper=model={Application.streamingAssetsPath}/Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" -f null -";
    private string ffmpegWindowsArguments = $"-f s16le -ar 48000 -ac 2 -i pipe:0 -vn -af \"whisper=model={Application.streamingAssetsPath}/Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" -f null -";
    private string ffmpegArguments;
    private int selectedMicrophoneIndex = 0;

    void Start()
    {
        // Check if system checks completed successfully
        var cfg = RuntimeGameConfig.Instance;
        if (cfg != null && !cfg.speechRecognitionAvailable)
        {
            UnityEngine.Debug.Log("Speech recognition is not available; speech-to-text processing will not be attempted");
            return;
        }

        // Check OS type
        UnityEngine.Debug.Log(SystemInfo.operatingSystem);
        if (SystemInfo.operatingSystem.Contains("Linux"))
        {
            // Uses Pulse audio library plugin
            UnityEngine.Debug.Log("Using Linux ffmpeg arguments.");
            ffmpegArguments = ffmpegLinuxArguments;
        }
        else if (SystemInfo.operatingSystem.Contains("Windows"))
        {
            // Uses s16le plugin
            UnityEngine.Debug.Log("Using Windows ffmpeg arguments");
            ffmpegArguments = ffmpegWindowsArguments;
        }
        
        // Handle microphone detection
        string microphone;
        if (Microphone.devices.Length > 0)
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
            {
                microphone = Microphone.devices[i];
                UnityEngine.Debug.Log(microphone);
            }
        }
        else
        {
            UnityEngine.Debug.Log("No devices found.");
        }

        // Initialize audio capture using config-saved microphone index
        InitializeAudioCapture();

        StartMicrophoneStreaming();
        UnityEngine.Debug.Log("Start function finished executing.");
    }

    private void InitializeAudioCapture()
    {
        if (Microphone.devices.Length == 0)
        {
            UnityEngine.Debug.LogError("No microphones found");
            return;
        }

        // Get microphone index from config instead of hardcoded value
        RuntimeGameConfig cfg = RuntimeGameConfig.Instance;
        selectedMicrophoneIndex = (cfg != null) ? cfg.selectedMicrophoneIndex : 0;
        
        // Validate index
        if (selectedMicrophoneIndex < 0 || selectedMicrophoneIndex >= Microphone.devices.Length)
        {
            selectedMicrophoneIndex = 0; // Default to first device
        }
        
        string micName = Microphone.devices[selectedMicrophoneIndex];
        UnityEngine.Debug.Log($"Using microphone: {micName}");

        audioClip = Microphone.Start(micName, true, 10, 48000);

        while (Microphone.GetPosition(micName) == 0)
            Thread.Sleep(100);

        audioBuffer = new byte[1024 * 2 * sizeof(float)];
    }

    void Update()
    {

        string currentOutput;
        lock (_lockObject)
        {
            currentOutput = _currentOutput;
        }

        if (!string.IsNullOrEmpty(currentOutput))
        {
            // Parse the recognized text into a number (1 or 2)
            if (int.TryParse(currentOutput, out int number) && (number == 1 || number == 2))
            {
                OnNumberRecognized?.Invoke(number);
            }
            else
            {
                // Try to parse numeric words "one" and "two"
                var lowerText = currentOutput.ToLower().Trim();
                if (lowerText == "one")
                    OnNumberRecognized?.Invoke(1);
                else if (lowerText == "two")
                    OnNumberRecognized?.Invoke(2);
            }
            
            // Clear the output after processing
            lock (_lockObject)
            {
                _currentOutput = "";
            }
        }
    }

    void OnDestroy()
    {
        StopStreaming();
    }

    private async void StartStreaming()
    {
        if (isStreaming)
        {
            UnityEngine.Debug.Log("Notice: Already streaming.");
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        isStreaming = true;

        try
        {
            // Check for FFmpeg installation and whisper model existence
            if (!CheckFFmpegAndModel())
            {
                UnityEngine.Debug.LogWarning("[MicrophoneManager] FFmpeg/Whisper not available - speech recognition disabled");
                UnityEngine.Debug.LogWarning("[MicrophoneManager] Math game will need to use alternative input method");
                isStreaming = false;
                return;
            }

            currentPipeName = $"UnityAudioPipe_{Guid.NewGuid()}";
            pipeServer = new NamedPipeServerStream(currentPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            UnityEngine.Debug.Log("Attempting start of FFmpeg...");
            ffmpegProcess = Process.Start(startInfo);

            string errorOutput = "";
            string standardOutput = "";

            ffmpegProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorOutput += e.Data + "\n";
                    //UnityEngine.Debug.LogError($"FFmpeg Error: {e.Data}");
                }
            };

            ffmpegProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    standardOutput += e.Data + "\n";
                    UnityEngine.Debug.Log($"FFmpeg Output: {e.Data}");

                    // Parse speech output - simplified for just numbers 1 and 2
                    ParseSpeechResult(e.Data);
                }
            };

            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.BeginOutputReadLine();

            UnityEngine.Debug.Log("Attempting connection to pipe...");
            await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token);
            UnityEngine.Debug.Log("Successfully connected to pipe.");

            UnityEngine.Debug.Log("Attempting start of audio capture loop...");
            await Task.Run(() => AudioCaptureLoop(cancellationTokenSource.Token), cancellationTokenSource.Token);
            UnityEngine.Debug.Log("Successfully started audio capture loop.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Streaming error: {ex.Message}");
        }
    }

    private bool CheckFFmpegAndModel()
    {
        // Check FFmpeg installation
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);
                if (process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError("FFmpeg not installed or not accessible");
                    return false;
                }
                else
                {
                    UnityEngine.Debug.Log("FFmpeg installed.");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"FFmpeg check failed: {ex.Message}");
            return false;
        }

        // Check whisper model file existence
        string modelPath = GetWhisperModelPath();
        if (!File.Exists(modelPath))
        {
            // Try to copy from Assets/Whisper to StreamingAssets/Whisper if in editor
            if (CheckAndCopyWhisperModel())
            {
                // Verify it exists now
                if (!File.Exists(modelPath))
                {
                    UnityEngine.Debug.LogError("Whisper model file not found after attempted copy: " + modelPath);
                    return false;
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Whisper model file not found: " + modelPath);
                return false;
            }
        }
        else
        {
            UnityEngine.Debug.Log("Whisper model successfully found");
        }

        return true;
    }

    private string GetWhisperModelPath()
    {
        #if UNITY_EDITOR
            // In editor, use the Assets path
            return Path.Combine(Application.dataPath, "Whisper", "ggml-medium.en.bin");
        #else
            // In builds, reference from streaming assets
            return Path.Combine(Application.streamingAssetsPath, "Whisper", "ggml-medium.en.bin");
        #endif
    }

    private bool CheckAndCopyWhisperModel()
    {
        string modelPath = GetWhisperModelPath();
        
        // If already exists in streaming assets, return true
        if (File.Exists(modelPath))
            return true;
            
        #if UNITY_EDITOR
            // Copy from Assets/Whisper to StreamingAssets/Whisper
            string sourcePath = Path.Combine(Application.dataPath, "Whisper", "ggml-medium.en.bin");
            UnityEngine.Debug.Log(sourcePath);
            if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
                File.Copy(sourcePath, modelPath);
                UnityEngine.Debug.Log("Copied whisper model to streaming assets");
                return true;
            }
        #else
            // In builds, check if model exists in streaming assets
            // This assumes you've manually copied it to StreamingAssets in build process
            return false;
        #endif
        
        return false;
    }

    private void ParseSpeechResult(string data)
    {
        try
        {
            string text = "";
            
            // Handle JSON format from Whisper (e.g., {"text":"(upbeat music)"})
            if (data.Trim().StartsWith("{") && data.Trim().EndsWith("}"))
            {
                // Extract the text field from JSON - handle both single and double quotes
                var textMatch = Regex.Match(data, @"""text""\s*:\s*[""']([^""']*)[""']");
                if (textMatch.Success)
                {
                    text = textMatch.Groups[1].Value;
                }
            }
            else
            {
                // If not JSON, treat entire data as text
                text = data.Trim();
            }

            if (string.IsNullOrEmpty(text))
                return;

            // Simplified parsing for just "1", "one", "2", "two"
            var lowerText = text.ToLower().TrimEnd('.', ',', '!', '?', ';', ':', '"', '\'');
            
            if (lowerText == "1" || lowerText == "one")
            {
                lock (_lockObject)
                {
                    _currentOutput = "1";
                }
            }
            else if (lowerText == "2" || lowerText == "two")
            {
                lock (_lockObject)
                {
                    _currentOutput = "2";
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to basic text extraction
            try
            {
                var match = Regex.Match(data, @"\b(?:1|2)\b");
                if (match.Success)
                {
                    lock (_lockObject)
                    {
                        _currentOutput = match.Value;
                    }
                }
            }
            catch (Exception innerEx)
            {
                UnityEngine.Debug.LogError($"Speech parsing error: {innerEx.Message}");
            }

            UnityEngine.Debug.LogError(ex + " - Falling back to basic text extraction");
        }
    }

    private async Task AudioCaptureLoop(CancellationToken cancellationToken)
    {
        int sampleSize = sizeof(float) * 2; // stereo
        var tempBuffer = new float[1024 * 2];

        while (!cancellationToken.IsCancellationRequested && isStreaming)
        {
            try
            {
                // Use the selected microphone index instead of hardcoded MIC_DEFAULT
                string micName = Microphone.devices[selectedMicrophoneIndex];
                int position = Microphone.GetPosition(micName);

                if (position >= 1024)
                {
                    audioClip.GetData(tempBuffer, 0);

                    var byteBuffer = new byte[1024 * sampleSize];
                    for (int i = 0; i < tempBuffer.Length; i++)
                    {
                        short sample = (short)(tempBuffer[i] * short.MaxValue);
                        BitConverter.GetBytes(sample).CopyTo(byteBuffer, i * sizeof(short));
                    }

                    await Task.Run(() =>
                    {
                        if (pipeServer != null && pipeServer.IsConnected)
                        {
                            pipeServer.Write(byteBuffer, 0, byteBuffer.Length);
                        }
                    }, cancellationToken);
                }

                await Task.Delay(10, cancellationToken);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Audio capture error: {ex.Message}");
                break;
            }
        }
    }

    private void StopStreaming()
    {
        isStreaming = false;
        cancellationTokenSource?.Cancel();

        try
        {
            cancellationTokenSource?.Dispose();
            pipeServer?.Dispose();

            if (ffmpegProcess != null)
            {
                try
                {
                    ffmpegProcess.StandardInput?.Close();
                }
                catch (Exception) { }

                try
                {
                    if (!ffmpegProcess.WaitForExit(3000))
                    {
                        ffmpegProcess.Kill();
                        ffmpegProcess.WaitForExit();
                    }
                }
                catch (Exception) { }

                ffmpegProcess.Dispose();
                ffmpegProcess = null;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error stopping streaming: {ex.Message}");
        }
    }

    public void StartMicrophoneStreaming()
    {
        StartStreaming();
    }

    public void StopMicrophoneStreaming()
    {
        StopStreaming();
    }
}
