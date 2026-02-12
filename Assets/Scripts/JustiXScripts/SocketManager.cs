using UnityEngine;
using SocketIOClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance;

    [Header("Config")]
    public string serverUrl = "http://YOUR_IP:5050";

    [Header("References")]
    public OppositionLawyer oppositionLawyer;
    public AudioSource judgeAudioSource;

    // Internal State
    private SocketIOUnity socket;
    private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        while (_executionQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    public void ConnectToSocket(string meetingId)
    {
        var uri = new Uri(serverUrl);

        socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            ConnectionTimeout = TimeSpan.FromSeconds(10)
        });

        socket.OnConnected += (sender, e) => {
            _executionQueue.Enqueue(() => {
                Debug.Log("Socket Connected Successfully!");
                socket.Emit("join_meeting", meetingId);
            });
        };

        socket.On("ai_response", (response) => {
            string jsonString = response.ToString();

            _executionQueue.Enqueue(() => {
                Debug.Log($"[RAW JSON]: {jsonString}");

                try
                {
                    // 1. Manually Parse the Array
                    var jsonArray = Newtonsoft.Json.Linq.JArray.Parse(jsonString);
                    var jsonObj = jsonArray[0]; // Get the first object

                    // 2. Extract Data Manually (Case-Insensitive)
                    AIResponse data = new AIResponse();
                    data.text = (string)jsonObj["text"];

                    // Check for "audio" or "Audio"
                    data.audio = (string)jsonObj["audio"] ?? (string)jsonObj["Audio"];

                    // Check for "speaker" or "Speaker"
                    data.speaker = (string)jsonObj["speaker"] ?? (string)jsonObj["Speaker"];

                    // Check for "emotion" or "Emotion"
                    data.emotion = (string)jsonObj["emotion"] ?? (string)jsonObj["Emotion"];

                    // 3. Audio Format Check
                    if (!string.IsNullOrEmpty(data.audio) && data.audio.StartsWith("//"))
                    {
                        Debug.LogError("CRITICAL: Backend sent MP3 data (starts with //). Unity needs WAV (starts with UklGR). Animation will play but NO AUDIO will be heard unless backend changes format.");
                    }

                    HandleAIResponse(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Manual Parsing Error: {ex.Message}");
                }
            });
        });

        socket.Connect();
    }

    public void SendAudioStream(string base64Audio)
    {
        if (socket == null || !socket.Connected)
        {
            Debug.LogError("Cannot send audio: Socket is NOT connected!");
            return;
        }

        // DEBUG LOG: Check if ID is present
        Debug.Log($"[Socket] Sending Audio... MeetingID: '{CaseManager.CurrentMeetingId}' | Audio Length: {base64Audio.Length}");

        var payload = new
        {
            meetingId = CaseManager.CurrentMeetingId,
            audio = base64Audio,
            speaker = "User"
        };

        socket.Emit("audio_stream", payload);
    }

    private void HandleAIResponse(AIResponse data)
    {
        Debug.Log($"AI Reply from: {data.speaker} | Emotion: {data.emotion}");

        AudioClip clip = GetClipFromBase64(data.audio);
        if (clip == null) return;

        if (data.speaker == "Opposing Lawyer")
        {
            oppositionLawyer.PlayResponse(clip, data.emotion);
        }
        else if (data.speaker == "Judge")
        {
            judgeAudioSource.clip = clip;
            judgeAudioSource.Play();
        }
    }

    private AudioClip GetClipFromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            byte[] audioBytes = Convert.FromBase64String(base64);
            return WavUtility.ToAudioClip(audioBytes);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to decode audio: " + e.Message);
            return null;
        }
    }

    private void OnDestroy()
    {
        if (socket != null)
        {
            socket.Disconnect();
            socket.Dispose();
        }
    }
}