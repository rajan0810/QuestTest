using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Collections.Generic;
using TMPro;

public class CaseManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject hashcodePanel;
    public TMP_InputField codeInputField; // Optional if using VR Keypad only
    public Button joinButton;             // Optional if using VR Keypad only
    public Image clipboardDisplay;        // The Image component on the VR Clipboard
    public Button nextButton;
    public Button prevButton;
    public GameObject loadingSpinner;

    [Header("Backend Config")]
    public string baseUrl = "http://YOUR_IP:5050"; // Make public to edit in Inspector easily

    // State Data
    public static string CurrentMeetingId { get; private set; }
    private List<Texture2D> evidenceTextures = new List<Texture2D>();
    private int currentPageIndex = 0;

    void Start()
    {
        // Safety checks to prevent crashes if things aren't assigned
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);

        UpdateEvidenceUI();
    }

    // --- Entry Point 1: Standard UI Button ---
    private async void OnJoinClicked()
    {
        if (codeInputField == null) return;
        string code = codeInputField.text;
        await HandleJoinLogic(code);
    }

    // --- Entry Point 2: VR Keypad (Call this from HashcodeManager) ---
    public void JoinMeetingFromKeypad(string code)
    {
        // "Fire and forget" async call compatible with Unity Events
        _ = HandleJoinLogic(code);
    }

    // Shared Logic
    private async Task HandleJoinLogic(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Meeting code is empty!");
            return;
        }

        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        await JoinSession(code);

        if (loadingSpinner != null) loadingSpinner.SetActive(false);
    }

    private async Task JoinSession(string code)
    {
        string url = $"{baseUrl}/api/cases/meeting/vr/join";

        JoinRequest req = new JoinRequest { meetingCode = code };
        string json = JsonUtility.ToJson(req);

        Debug.Log($"Attempting to join with code: {code} at {url}");

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone) await Task.Yield();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Join Success: " + webRequest.downloadHandler.text);
                JoinResponse response = JsonUtility.FromJson<JoinResponse>(webRequest.downloadHandler.text);

                if (hashcodePanel != null)
                {
                    hashcodePanel.SetActive(false); // Hides the keypad
                }

                // 1. Store Meeting ID
                CurrentMeetingId = response.meetingId;

                // 2. Start Downloading Evidence
                if (response.evidencePages != null && response.evidencePages.Length > 0)
                {
                    await DownloadEvidenceImages(response.evidencePages);
                }
                else
                {
                    Debug.Log("No evidence pages found for this case.");
                }

                // 3. Trigger Socket Connection
                // Checks if SocketManager exists before calling to prevent errors
                if (SocketManager.Instance != null)
                {
                    Debug.Log($"Connecting to Socket with Room ID: {CurrentMeetingId}");
                    SocketManager.Instance.ConnectToSocket(CurrentMeetingId);
                }
                else
                {
                    Debug.LogError("SocketManager Instance not found! Is the script attached to a GameObject?");
                }
            }
            else
            {
                Debug.LogError($"Join Failed: {webRequest.error} | Response: {webRequest.downloadHandler.text}");
            }
        }
    }

    private async Task DownloadEvidenceImages(string[] urls)
    {
        evidenceTextures.Clear();

        foreach (string url in urls)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(request);
                    evidenceTextures.Add(tex);
                }
                else
                {
                    Debug.LogError($"Failed to download image: {url}");
                }
            }
        }

        // Reset to page 1
        currentPageIndex = 0;
        UpdateEvidenceUI();
    }

    // --- Clipboard Navigation ---

    private void UpdateEvidenceUI()
    {
        if (clipboardDisplay == null) return;

        if (evidenceTextures.Count == 0)
        {
            clipboardDisplay.sprite = null;
            clipboardDisplay.color = Color.gray; // Visual cue that it's empty
            return;
        }

        clipboardDisplay.color = Color.white;

        // Convert Texture2D to Sprite
        Texture2D tex = evidenceTextures[currentPageIndex];
        // Create a new sprite (Pivot in center)
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        clipboardDisplay.sprite = sprite;
    }

    public void NextPage()
    {
        if (evidenceTextures.Count == 0) return;
        currentPageIndex = (currentPageIndex + 1) % evidenceTextures.Count;
        UpdateEvidenceUI();
    }

    public void PrevPage()
    {
        if (evidenceTextures.Count == 0) return;
        currentPageIndex--;
        if (currentPageIndex < 0) currentPageIndex = evidenceTextures.Count - 1;
        UpdateEvidenceUI();
    }
}