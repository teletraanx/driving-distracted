using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public class MicrophoneManager : MonoBehaviour
{
    private AudioClip audioClip;
    private Process ffmpegProcess;
    private CancellationTokenSource cancellationTokenSource;
    private bool isStreaming = false;
    private Task streamingTask;

    private readonly object _lockObject = new();
    private string _currentOutput = "";

    public event Action<int> OnNumberRecognized;
    public event Action OnUnrecognizedSpeech;

    private string ffmpegWindowsArguments =
        "-f s16le -ar 48000 -ac 2 -i - " +
        "-vn -af \"whisper=model=Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" " +
        "-f null -";

    private string ffmpegLinuxArguments =
        "-f s16le -ar 48000 -ac 2 -i - " +
        "-vn -af \"whisper=model=Whisper/ggml-medium.en.bin:language=en:queue=3:destination=-:format=json\" " +
        "-f null -";

    private string ffmpegArguments;
    private int selectedMicrophoneIndex = 0;

    private string micName;
    private int lastSamplePosition = 0;
    private const int chunkSamples = 1024;

    [Header("Mic Debug")]
    public float currentLevel = 0f;        
    public float lastWhisperTime = -1f;    
    public string lastWhisperText = "";    

    public bool IsStreaming => isStreaming;


    private void Start()
    {
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

        InitializeAudioCapture();
        StartMicrophoneStreaming();
        UnityEngine.Debug.Log("[Mic] MicrophoneManager initialized and streaming started.");
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

        micName = Microphone.devices[selectedMicrophoneIndex];
        UnityEngine.Debug.Log($"[Mic] Using microphone: {micName}");

        audioClip = Microphone.Start(micName, true, 10, 48000);

        while (Microphone.GetPosition(micName) == 0)
            Thread.Sleep(100);

        lastSamplePosition = 0;
    }

    private void Update()
    {
        string currentOutput;

        lock (_lockObject)
        {
            currentOutput = _currentOutput;
            _currentOutput = "";
        }

        if (!string.IsNullOrEmpty(currentOutput))
        {
            HandleRecognizedText(currentOutput);
        }
    }

    private void OnDestroy()
    {
        StopStreaming();
    }

    public void StartMicrophoneStreaming()
    {
        if (isStreaming)
        {
            UnityEngine.Debug.Log("[Mic] Streaming already active.");
            return;
        }

        streamingTask = StartStreaming();
    }

    public void StopMicrophoneStreaming()
    {
        StopStreaming();
    }

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

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                WorkingDirectory = Application.streamingAssetsPath
            };

            UnityEngine.Debug.Log("[Mic] Attempting start of FFmpeg...");
            ffmpegProcess = Process.Start(startInfo);

            DataReceivedEventHandler errorHandler = (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    UnityEngine.Debug.Log("[FFmpeg ERR] " + e.Data);
                }
            };

            DataReceivedEventHandler outputHandler = (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    UnityEngine.Debug.Log("[FFmpeg OUT] " + e.Data);
                    ParseSpeechResult(e.Data);
                }
            };

            ffmpegProcess.ErrorDataReceived += errorHandler;
            ffmpegProcess.OutputDataReceived += outputHandler;

            ffmpegProcess.BeginErrorReadLine();
            ffmpegProcess.BeginOutputReadLine();

            UnityEngine.Debug.Log("[Mic] Starting audio capture coroutine...");
            StartCoroutine(AudioCaptureLoopCoroutine());
            await Task.Yield();
        }
        catch (OperationCanceledException)
        {
            UnityEngine.Debug.Log("Streaming canceled.");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Streaming error: {ex.Message}");
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
            if (string.IsNullOrWhiteSpace(data))
                return;

            string trimmed = data.Trim();

            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
            {
                return;
            }

            var textMatch = Regex.Match(trimmed, @"""text""\s*:\s*[""']([^""']*)[""']");
            if (!textMatch.Success)
                return;

            string text = textMatch.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(text))
                return;

            string lower = text.ToLowerInvariant();
            if (lower.Contains("static crackling") ||
                lower.Contains("static") ||
                lower.Contains("noise") ||
                lower.Contains("breathing") ||
                lower.Contains("silence") ||
                lower.Contains("clicking"))
            {
                return;
            }

            lock (_lockObject)
            {
                _currentOutput = text;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Mic] ParseSpeechResult error: {ex}");
        }
    }

    private System.Collections.IEnumerator AudioCaptureLoopCoroutine()
    {
        while (audioClip == null)
        {
            yield return null;
        }

        int channels = audioClip.channels;
        int samplesPerChunk = chunkSamples;
        int totalClipSamples = audioClip.samples;
        int floatsPerChunk = samplesPerChunk * channels;

        float[] tempBuffer = new float[floatsPerChunk];
        byte[] byteBuffer = new byte[floatsPerChunk * 2];

        UnityEngine.Debug.Log("[Mic] AudioCaptureLoopCoroutine started.");

        while (true)
        {
            if (!isStreaming || ffmpegProcess == null || ffmpegProcess.HasExited)
            {
                yield return null;
                continue;
            }

            int micPosition = Microphone.GetPosition(micName);
            if (micPosition < 0)
            {
                yield return null;
                continue;
            }

            int samplesAvailable = micPosition - lastSamplePosition;
            if (samplesAvailable < 0)
                samplesAvailable += totalClipSamples;

            if (samplesAvailable < samplesPerChunk)
            {
                yield return null;
                continue;
            }

            audioClip.GetData(tempBuffer, lastSamplePosition);

            lastSamplePosition += samplesPerChunk;
            if (lastSamplePosition >= totalClipSamples)
                lastSamplePosition -= totalClipSamples;

            float sumSq = 0f;
            for (int i = 0; i < floatsPerChunk; i++)
            {
                float f = Mathf.Clamp(tempBuffer[i], -1f, 1f);
                sumSq += f * f;

                short sample = (short)(f * short.MaxValue);
                int byteIndex = i * 2;
                byteBuffer[byteIndex] = (byte)(sample & 0xff);
                byteBuffer[byteIndex + 1] = (byte)((sample >> 8) & 0xff);
            }
            currentLevel = Mathf.Sqrt(sumSq / floatsPerChunk);

            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                try
                {
                    ffmpegProcess.StandardInput.BaseStream.Write(byteBuffer, 0, byteBuffer.Length);
                    ffmpegProcess.StandardInput.BaseStream.Flush();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Mic] Error writing to ffmpeg stdin: {ex.Message}");
                }
            }

            yield return new WaitForSeconds(0.01f);
        }
    }

    private void StopStreaming()
    {
        isStreaming = false;
        cancellationTokenSource?.Cancel();

        try
        {
            try
            {
                ffmpegProcess?.CancelOutputRead();
                ffmpegProcess?.CancelErrorRead();
            }
            catch { }

            cancellationTokenSource?.Dispose();

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

    private void HandleRecognizedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        lastWhisperTime = Time.time;
        lastWhisperText = text;

        var cfg = RuntimeGameConfig.Instance;
        if (cfg == null || cfg.inputMode != InputMode.Microphone)
        {
            return;
        }

        string lower = text.ToLowerInvariant();
        UnityEngine.Debug.Log($"[Mic] Recognized text: \"{lower}\"");

        int value;
        if (TryMapTextToNumber(lower, out value))
        {
            UnityEngine.Debug.Log($"[Mic] Mapped recognized text to number: {value}");
            OnNumberRecognized?.Invoke(value);
        }
    }

    private bool TryMapTextToNumber(string text, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim().ToLowerInvariant();
        string bare = text.Trim('.', ',', '!', '?', ';', ':', '"', '\'');

        if (bare == "one" || bare == "1" || bare == "when")
        {
            number = 1;
            return true;
        }

        if (bare.Contains("one") || bare.Contains("when") || bare.Contains("bye"))
        {
            number = 1;
            return true;
        }

        if (bare == "two" || bare == "2")
        {
            number = 2;
            return true;
        }

        if (bare.Contains("two") || bare.Contains(" to ") || bare.Contains("too") ||
            bare.Contains("here") || bare.Contains("clear") || bare.Contains("thank you"))
        {
            number = 2;
            return true;
        }

        UnityEngine.Debug.Log("[Mic] Speech did not match any valid option.");
        OnUnrecognizedSpeech?.Invoke();
        return false;
    }
}
