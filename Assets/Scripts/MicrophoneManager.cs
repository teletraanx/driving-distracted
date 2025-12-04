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
    private AudioClip audioClip;
    private byte[] audioBuffer;
    private NamedPipeServerStream pipeServer;
    private Process ffmpegProcess;
    private CancellationTokenSource cancellationTokenSource;
    private bool isStreaming = false;
    private string currentPipeName = "";
    private Task streamingTask;

    private readonly object _lockObject = new();
    private string _currentOutput = "";

    public event Action<int> OnNumberRecognized;

    private string ffmpegLinuxArguments = $"-f pulse -ar 48000 -ac 2 -i pipe:0 -vn -af \"whisper=model={Application.streamingAssetsPath}/Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" -f null -";
    private string ffmpegWindowsArguments = $"-f s16le -ar 48000 -ac 2 -i pipe:0 -vn -af \"whisper=model={Application.streamingAssetsPath}/Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" -f null -";
    private string ffmpegArguments;
    private int selectedMicrophoneIndex = 0;

    void Start()
    {
        var cfg = RuntimeGameConfig.Instance;
        if (cfg != null && !cfg.speechRecognitionAvailable)
        {
            UnityEngine.Debug.Log("Speech recognition is not available; speech-to-text processing will not be attempted");
            return;
        }

        UnityEngine.Debug.Log(SystemInfo.operatingSystem);
        if (SystemInfo.operatingSystem.Contains("Linux"))
        {
            UnityEngine.Debug.Log("Using Linux ffmpeg arguments.");
            ffmpegArguments = ffmpegLinuxArguments;
        }
        else if (SystemInfo.operatingSystem.Contains("Windows"))
        {
            UnityEngine.Debug.Log("Using Windows ffmpeg arguments");
            ffmpegArguments = ffmpegWindowsArguments;
        }

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

        RuntimeGameConfig cfg = RuntimeGameConfig.Instance;
        selectedMicrophoneIndex = (cfg != null) ? cfg.selectedMicrophoneIndex : 0;

        if (selectedMicrophoneIndex < 0 || selectedMicrophoneIndex >= Microphone.devices.Length)
        {
            selectedMicrophoneIndex = 0;
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
            if (int.TryParse(currentOutput, out int number) && (number == 1 || number == 2))
            {
                OnNumberRecognized?.Invoke(number);
            }
            else
            {
                var lowerText = currentOutput.ToLower().Trim();
                if (lowerText == "one")
                    OnNumberRecognized?.Invoke(1);
                else if (lowerText == "two")
                    OnNumberRecognized?.Invoke(2);
            }

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

    // ---------------------------
    // FIXED — Safe async startup
    // ---------------------------
    private async Task StartStreaming()
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
            if (!CheckFFmpegAndModel())
            {
                UnityEngine.Debug.LogWarning("[MicrophoneManager] FFmpeg/Whisper not available - speech recognition disabled");
                isStreaming = false;
                return;
            }

            currentPipeName = $"UnityAudioPipe_{Guid.NewGuid()}";
            pipeServer = new NamedPipeServerStream(currentPipeName, PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

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

            DataReceivedEventHandler errorHandler = (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // UnityEngine.Debug.Log(e.Data);
                }
            };

            DataReceivedEventHandler outputHandler = (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    ParseSpeechResult(e.Data);
                    UnityEngine.Debug.Log(e.Data);
                }
            };

            ffmpegProcess.ErrorDataReceived += errorHandler;
            ffmpegProcess.OutputDataReceived += outputHandler;

            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.BeginOutputReadLine();

            UnityEngine.Debug.Log("Attempting connection to pipe...");

            try
            {
                await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token);
                UnityEngine.Debug.Log("Successfully connected to pipe.");
            }
            catch (OperationCanceledException)
            {
                UnityEngine.Debug.Log("Pipe connection canceled.");
                return;
            }
            catch (ObjectDisposedException)
            {
                UnityEngine.Debug.Log("Pipe was disposed before connection finished (normal on shutdown).");
                return;
            }

            UnityEngine.Debug.Log("Attempting start of audio capture loop...");
            await Task.Run(
                () => AudioCaptureLoop(cancellationTokenSource.Token),
                cancellationTokenSource.Token
            );
            UnityEngine.Debug.Log("Audio capture loop ended.");
        }
        catch (OperationCanceledException)
        {
            UnityEngine.Debug.Log("Streaming canceled.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Streaming error: {ex.Message}");
        }
        finally
        {
            isStreaming = false;
        }
    }

    private bool CheckFFmpegAndModel()
    {
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

        string modelPath = GetWhisperModelPath();
        if (!File.Exists(modelPath))
        {
            if (!CheckAndCopyWhisperModel())
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
        return Path.Combine(Application.dataPath, "Whisper", "ggml-medium.en.bin");
#else
        return Path.Combine(Application.streamingAssetsPath, "Whisper", "ggml-medium.en.bin");
#endif
    }

    private bool CheckAndCopyWhisperModel()
    {
        string modelPath = GetWhisperModelPath();

        if (File.Exists(modelPath))
            return true;

#if UNITY_EDITOR
        string sourcePath = Path.Combine(Application.dataPath, "Whisper", "ggml-medium.en.bin");
        if (File.Exists(sourcePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            File.Copy(sourcePath, modelPath);
            UnityEngine.Debug.Log("Copied whisper model to streaming assets");
            return true;
        }
#endif

        return false;
    }

    private void ParseSpeechResult(string data)
    {
        try
        {
            string text = "";

            if (data.Trim().StartsWith("{") && data.Trim().EndsWith("}"))
            {
                var textMatch = Regex.Match(data, @"""text""\s*:\s*[""']([^""']*)[""']");
                if (textMatch.Success)
                {
                    text = textMatch.Groups[1].Value;
                }
            }
            else
            {
                text = data.Trim();
            }

            if (string.IsNullOrEmpty(text))
                return;

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
        catch { }
    }

    private async Task AudioCaptureLoop(CancellationToken cancellationToken)
    {
        int sampleSize = sizeof(float) * 2;
        var tempBuffer = new float[1024 * 2];

        while (!cancellationToken.IsCancellationRequested && isStreaming)
        {
            try
            {
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Audio capture error: {ex.Message}");
                break;
            }
        }
    }

    // ---------------------------
    // FIXED — Graceful shutdown
    // ---------------------------
    private void StopStreaming()
    {
        isStreaming = false;
        cancellationTokenSource?.Cancel();

        try
        {
            if (streamingTask != null && !streamingTask.IsCompleted)
            {
                Task.WaitAny(streamingTask, Task.Delay(2000));
            }
        }
        catch { }

        try
        {
            try
            {
                ffmpegProcess?.CancelOutputRead();
                ffmpegProcess?.CancelErrorRead();
            }
            catch { }

            cancellationTokenSource?.Dispose();

            if (pipeServer != null)
            {
                try
                {
                    if (pipeServer.IsConnected)
                        pipeServer.Disconnect();
                }
                catch { }

                pipeServer.Dispose();
                pipeServer = null;
            }

            if (ffmpegProcess != null)
            {
                try { ffmpegProcess.StandardInput?.Close(); } catch { }

                try
                {
                    if (!ffmpegProcess.WaitForExit(3000))
                    {
                        ffmpegProcess.Kill();
                        ffmpegProcess.WaitForExit();
                    }
                }
                catch { }

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
        streamingTask = StartStreaming();
    }

    public void StopMicrophoneStreaming()
    {
        StopStreaming();
    }
}
