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
    public TMP_InputField codeInputField;
    public Button joinButton;
    public Image clipboardDisplay;
    public Button nextButton;
    public Button prevButton;
    public GameObject loadingSpinner;

    [Header("End Session UI")]
    public Button endButton;
    public GameObject endButtonObject;

    [Header("Judge Audio")]
    public AudioSource judgeAudioSource;
    public AudioClip sessionEndClip;

    [Header("Backend Config")]
    public string baseUrl = "http://YOUR_IP:5050";

    // State Data
    public static string CurrentMeetingId { get; private set; }
    private List<Texture2D> evidenceTextures = new List<Texture2D>();
    private int currentPageIndex = 0;

    void Start()
    {
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (endButton != null) endButton.onClick.AddListener(OnEndSessionClicked);

        if (endButtonObject != null) endButtonObject.SetActive(false);
        UpdateEvidenceUI();
    }

    private async void OnJoinClicked()
    {
        if (codeInputField == null) return;
        await HandleJoinLogic(codeInputField.text);
    }

    public void JoinMeetingFromKeypad(string code) => _ = HandleJoinLogic(code);

    private async Task HandleJoinLogic(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        if (loadingSpinner != null) loadingSpinner.SetActive(true);
        await JoinSession(code);
        if (loadingSpinner != null) loadingSpinner.SetActive(false);
    }

    private async Task JoinSession(string code)
    {
        string url = $"{baseUrl}/api/cases/meeting/vr/join";
        JoinRequest req = new JoinRequest { meetingCode = code };
        string json = JsonUtility.ToJson(req);

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
                JoinResponse response = JsonUtility.FromJson<JoinResponse>(webRequest.downloadHandler.text);
                CurrentMeetingId = response.meetingId;

                if (hashcodePanel != null) hashcodePanel.SetActive(false);
                if (endButtonObject != null) endButtonObject.SetActive(true);

                if (response.evidencePages != null && response.evidencePages.Length > 0)
                    await DownloadEvidenceImages(response.evidencePages);

                if (SocketManager.Instance != null)
                    SocketManager.Instance.ConnectToSocket(CurrentMeetingId);
            }
        }
    }

    // --- Optimized End Session Logic ---
    private void OnEndSessionClicked()
    {
        // 1. Play Judge's Audio immediately
        if (judgeAudioSource != null && sessionEndClip != null)
        {
            judgeAudioSource.clip = sessionEndClip;
            judgeAudioSource.Play();
        }

        // 2. Visual Feedback
        if (endButton != null) endButton.interactable = false;

        // 3. Fire and Forget the request
        StartCoroutine(SendEndRequest());
    }

    private System.Collections.IEnumerator SendEndRequest()
    {
        string url = $"{baseUrl}/api/cases/meeting/end";
        EndSessionRequest req = new EndSessionRequest { meetingId = CurrentMeetingId };
        string json = JsonUtility.ToJson(req);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("End Session Request Sent Successfully.");
            }
            else
            {
                Debug.LogError("Failed to send End Session request: " + webRequest.error);
                if (endButton != null) endButton.interactable = true;
            }
        }
    }

    // --- Evidence Navigation ---
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
                    evidenceTextures.Add(DownloadHandlerTexture.GetContent(request));
            }
        }
        currentPageIndex = 0;
        UpdateEvidenceUI();
    }

    private void UpdateEvidenceUI()
    {
        if (clipboardDisplay == null) return;
        if (evidenceTextures.Count == 0) { clipboardDisplay.sprite = null; return; }
        Texture2D tex = evidenceTextures[currentPageIndex];
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        clipboardDisplay.sprite = sprite;
    }

    public void NextPage() { if (evidenceTextures.Count > 0) { currentPageIndex = (currentPageIndex + 1) % evidenceTextures.Count; UpdateEvidenceUI(); } }
    public void PrevPage() { if (evidenceTextures.Count > 0) { currentPageIndex = (currentPageIndex - 1 + evidenceTextures.Count) % evidenceTextures.Count; UpdateEvidenceUI(); } }
}