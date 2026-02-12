using UnityEngine;
using TMPro;

public class HashcodeManager : MonoBehaviour
{
    [Header("References")]
    public CaseManager caseManager; // Drag your NetworkManager here

    [Header("UI References")]
    public TextMeshProUGUI codeText;

    void Start()
    {
        if (codeText != null)
            codeText.text = "";
    }

    public void AddNum(string num)
    {
        if (codeText != null)
        {
            if (codeText.text.Length < 6) // Optional: Limit to 6 digits
                codeText.text += num;
        }
    }

    public void ClearCode()
    {
        if (codeText != null)
            codeText.text = "";
    }

    public void SubmitCode()
    {
        if (caseManager != null)
        {
            // 1. Get the code from the UI
            string finalCode = codeText.text;

            // 2. Send it to the CaseManager logic
            Debug.Log($"Submitting Code: {finalCode}");
            caseManager.JoinMeetingFromKeypad(finalCode);

            // 3. Optional: Clear after submit
            // ClearCode(); 
        }
        else
        {
            Debug.LogError("CaseManager reference is missing on HashcodeManager!");
        }
    }
}