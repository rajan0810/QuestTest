using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class MicrophoneManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The button the user clicks to start/stop speaking")]
    public Button speakButton;

    [Tooltip("The background image component of the button (to change color)")]
    public Image buttonImage;

    [Tooltip("The text component on the button")]
    public Text buttonText;

    [Tooltip("The raw image component for the microphone icon")]
    public RawImage iconImage;

    [Header("UI Assets")]
    public Texture mutedIcon;   // Drag your 'Muted' icon texture here
    public Texture unmutedIcon; // Drag your 'Unmuted' icon texture here
    public Color normalColor = new Color(0f, 0.5f, 1f, 1f); // Default Blue
    public Color recordingColor = Color.green;

    [Header("Audio Settings")]
    public int frequency = 44100; // Standard quality
    public int maxRecordingTime = 30; // Max seconds before auto-stop

    // Private State
    private AudioClip recordingClip;
    private bool isRecording = false;
    private string micDevice;

    void Start()
    {
        // 1. Initialize UI to Default State
        ResetUI();

        // 2. Check for Microphone Availability
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            Debug.Log($"Microphone Found: {micDevice}");

            // Add listener to the button
            speakButton.onClick.AddListener(ToggleRecording);
        }
        else
        {
            Debug.LogError("No Microphone detected!");
            buttonText.text = "No Mic";
            speakButton.interactable = false;
        }
    }

    // --- Interaction Logic ---

    public void ToggleRecording()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecordingAndSend();
        }
    }

    private void StartRecording()
    {
        isRecording = true;

        // 1. Update UI
        buttonImage.color = recordingColor;
        buttonText.text = "Recording...";
        if (unmutedIcon != null) iconImage.texture = unmutedIcon;

        // 2. Start Unity Microphone
        // null device uses default. Loop = false.
        recordingClip = Microphone.Start(micDevice, false, maxRecordingTime, frequency);
    }

    private void StopRecordingAndSend()
    {
        if (!isRecording) return;

        isRecording = false;

        // 1. Capture the position BEFORE stopping to know how long the user spoke
        int lastPos = Microphone.GetPosition(micDevice);

        // 2. Stop Microphone
        Microphone.End(micDevice);

        // 3. Reset UI immediately for feedback
        ResetUI();

        // 4. Validate recording length
        if (lastPos <= 0)
        {
            Debug.LogWarning("Recording was too short or empty.");
            return;
        }

        // 5. Process in Coroutine (to avoid freezing UI)
        StartCoroutine(ProcessAndSendAudio(recordingClip, lastPos));
    }

    private void ResetUI()
    {
        buttonImage.color = normalColor;
        buttonText.text = "Speak";
        if (mutedIcon != null) iconImage.texture = mutedIcon;
    }

    // --- Audio Processing Logic ---

    private IEnumerator ProcessAndSendAudio(AudioClip clip, int validSampleCount)
    {
        Debug.Log("Processing Audio...");

        // 1. Extract the valid samples (Trim the clip)
        // Unity creates a clip of 'maxRecordingTime' length. We only want what was spoken.
        float[] allSamples = new float[clip.samples * clip.channels];
        clip.GetData(allSamples, 0);

        // Calculate actual length used
        float[] validSamples = new float[validSampleCount * clip.channels];
        Array.Copy(allSamples, validSamples, validSamples.Length);

        // 2. Convert to WAV Bytes using our helper
        // This adds the RIFF header so the backend recognizes it as a WAV file
        byte[] wavBytes = WavUtility.FromAudioClip(validSamples, frequency, clip.channels);

        // 3. Convert to Base64 String
        string base64Audio = Convert.ToBase64String(wavBytes);

        Debug.Log($"Audio Processed. Bytes: {wavBytes.Length}. Sending to Socket...");

        // 4. Send to Backend via SocketManager
        if (SocketManager.Instance != null)
        {
            SocketManager.Instance.SendAudioStream(base64Audio);
        }
        else
        {
            Debug.LogError("SocketManager Instance is null! Cannot send audio.");
        }

        yield return null;
    }
}