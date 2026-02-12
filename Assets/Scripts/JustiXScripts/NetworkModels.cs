using System;
using System.Collections.Generic;

[Serializable]
public class JoinRequest
{
    public string meetingCode;
}

[Serializable]
public class JoinResponse
{
    public bool success;
    public string meetingId;
    public string caseId;
    public string caseTitle;
    public string caseSummary;
    public string[] evidencePages; // URLs
}

[Serializable]
public class EndSessionRequest
{
    public string meetingCode;
}

[Serializable]
public class EndSessionResponse
{
    public bool success;
    public string summary;
    public int score;
    public string feedback;
}

// For Phase 3 (Socket) - We define this now to be ready
[Serializable]
public class AIResponse
{
    public string text;
    public string audio; // Base64 MP3
    public string speaker; // "Opposing Lawyer" or "Judge"
    public string emotion; // "aggressive", "neutral", etc.
}