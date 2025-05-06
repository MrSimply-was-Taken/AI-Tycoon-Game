using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Linq;

[System.Serializable]
public class OllamaRequest
{
    public string model;
    public string prompt;
    public bool stream;
}

public class OllamaAPI : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_Text resultText;
    public Button sendButton;

    public Button statsButton;
    public Button continueButton;

    public int money = 1000;
    public int reputation = 10;
    public int risk = 0;

    public TMP_Text moneyText;
    public TMP_Text reputationText;
    public TMP_Text riskText;

    public Button choice1Button;
    public Button choice2Button;
    public Button choice3Button;
    public TMP_Text choice1Text;
    public TMP_Text choice2Text;
    public TMP_Text choice3Text;

    string ollamaURL = "http://localhost:11434/api/generate";
    string model = "llama2";

    public string introContext =
@"You are running a text-based business tycoon simulation. The player is an entrepreneur making business decisions to grow (or sometimes risk!) their company.

GAME MECHANICS AND RULES:

- The player starts with three key stats: 
    • Money (in $)
    • Reputation (as an integer score)
    • Risk (as a %; measures their legal exposure/danger)
- Choices can be either legal or illegal. Illegal actions often provide faster rewards or higher risks.
- Each turn, the player selects from three distinct business actions you provide.
- The outcome of each decision affects Money, Reputation, and/or Risk (can be +, -, or 0).
- Illegal actions usually increase Risk and may lower Reputation.
- Legal actions tend to increase Reputation or Money more slowly, and carry less Risk.
- The game should present dramatic, realistic, or creative scenarios: managing employees, making investments, launching new products, bribing officials, tax fraud, hiring, partnerships, criminal activity, etc.
- Adapt available choices and stat changes according to the player’s current status and the previous turn’s choice.
- The narrative may end if Risk becomes extremely high, Reputation drops too low, or Money is lost.
- IMPORTANT LOGIC:
    • Never present a choice where Money spent is more than the player's current Money.
    • All choices must represent actions that make sense for a real business and given player stats.
    • Never allow stats to go below zero due to a choice.
    • Do not suggest impossible actions based on current stats.

IMPORTANT: Strictly follow the OUTPUT FORMAT for every request.
- When asked for business choices (a turn), only use the provided choice format with stat changes (see below).
- When asked for current player stats, always quote stats in this format: Money: ($Amount), Reputation: (X), Risk: (Y%).

FORMATTING RULES:
When giving business choices:
    - Each stat change must always show a plus or minus sign (+ or -), even when positive.
    - Always show the units: $ for Money and % for Risk (e.g., Money [+250], Risk [-2%]).
    - Stat change numbers must be realistic and make sense in the context of the action and current player stats.
    - Do not omit the sign, do not use alternate forms, do not summarize or add extra commentary.

When showing current player stats:
    - ONLY respond with a single line in this exact format: Money: ($Amount), Reputation: (X), Risk: (Y%)
    - Money is prefixed with $, Reputation is an integer, Risk is a percent with %.
    - No explanations, narration, or extra lines—just the stats line, exactly as instructed.

REMEMBER: Never break role. Only use the requested format for each situation.
";

    public string turnFormat =
@"RESPONSE FORMAT FOR THIS TURN:
- First, output ONE short summary sentence showing the result of the player's last action.
- Then output EXACTLY three choices, each alone on its own line, using this format:
1. [Short action description] (Money [+/-N], Reputation [+/-M], Risk [+/-P%])
2. [Short action description] (Money [+/-N], Reputation [+/-M], Risk [+/-P%])
3. [Short action description] (Money [+/-N], Reputation [+/-M], Risk [+/-P%])
- Each stat change must always show + or -, and always show the units $ and % as shown (e.g., Money [+200], Risk [-1%]).
- Stat amount must always include a sign even if positive.
- Only use realistic numbers for stat changes, based on the context and current player stats.
- Never allow stats to go below zero, or Money to become negative after a choice.
- Never list an option where Money change is a negative value greater than the player's current Money.
- Do not show narration or any explanation except the first summary sentence and the three numbered choices.
- If you break this format, the game ends immediately.
- Do NOT summarize numbers or use symbols alone (such as $400, +2 Reputation, +3 Risk): always use ""Money [+/-N], Reputation [+/-M], Risk [+/-P%]"".
";

    public string statFormat =
@"RESPONSE FORMAT FOR STATS:
Show the player's current stats EXACTLY in this format:
Money: ($CurrentAmount), Reputation: (CurrentAmount), Risk: (CurrentAmount%)
Where:
- Money is prefixed with $, e.g., $1320
- Reputation is just a number, e.g., 12
- Risk always has a percent sign, e.g., 22%
Do not show any explanation, summary, or extra lines, just the stats in a single line using the correct units.
";


    private bool waitingForAI = false;
    private bool isFirstPrompt = true;
    private bool statsMode = false;
    private string lastPlayerPrompt = "";

    void Start()
    {
        if (inputField == null || resultText == null || sendButton == null ||
            choice1Button == null || choice2Button == null || choice3Button == null ||
            choice1Text == null || choice2Text == null || choice3Text == null ||
            statsButton == null || continueButton == null)
        {
            Debug.LogError("UI fields not set in Inspector!");
            if (resultText != null)
                resultText.text = "UI not wired up!";
            return;
        }

        sendButton.onClick.AddListener(OnSendButtonClicked);
        choice1Button.onClick.AddListener(() => OnChoiceSelected(choice1Text.text));
        choice2Button.onClick.AddListener(() => OnChoiceSelected(choice2Text.text));
        choice3Button.onClick.AddListener(() => OnChoiceSelected(choice3Text.text));
        statsButton.onClick.AddListener(OnStatsButtonClicked);
        continueButton.onClick.AddListener(OnContinueButtonClicked);

        ShowChoiceButtons();
        DisableAllChoiceButtons();

        continueButton.gameObject.SetActive(false);

        resultText.text = "Ready!";
        UpdateStatDisplay();
        SendIntroPrompt();
    }

    void SendIntroPrompt()
    {
        isFirstPrompt = true;
        inputField.text = "";
        resultText.text = "Thinking...";
        DisableAllChoiceButtons();
        statsButton.interactable = false;
        continueButton.gameObject.SetActive(false);

        lastPlayerPrompt = "Start a new game. Present three different businesses for me to choose from.";
        StartCoroutine(SendPrompt(lastPlayerPrompt, true));
    }

    void ShowChoiceButtons()
    {
        choice1Button.gameObject.SetActive(true);
        choice2Button.gameObject.SetActive(true);
        choice3Button.gameObject.SetActive(true);
    }

    void DisableAllChoiceButtons()
    {
        choice1Button.interactable = false;
        choice2Button.interactable = false;
        choice3Button.interactable = false;
        choice1Text.text = "...";
        choice2Text.text = "...";
        choice3Text.text = "...";
    }

    void EnableAllChoiceButtons()
    {
        choice1Button.interactable = true;
        choice2Button.interactable = true;
        choice3Button.interactable = true;
    }

    void OnSendButtonClicked()
    {
        if (waitingForAI) return;
        string prompt = inputField.text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            resultText.text = "Please enter your action!";
            return;
        }
        lastPlayerPrompt = prompt;
        inputField.text = "";
        resultText.text = "Thinking...";

        DisableAllChoiceButtons();
        statsButton.interactable = false;
        continueButton.gameObject.SetActive(false);

        StartCoroutine(SendPrompt(prompt, false));
    }

    void OnChoiceSelected(string fullChoiceText)
    {
        if (waitingForAI) return;
        waitingForAI = true;
        DisableAllChoiceButtons();
        statsButton.interactable = false;
        continueButton.gameObject.SetActive(false);

        if (CanApplyChoiceEffect(fullChoiceText, out string reason))
        {
            ApplyChoiceEffect(fullChoiceText);
            UpdateStatDisplay();
            resultText.text = "Thinking...";
            inputField.text = "";
            lastPlayerPrompt = fullChoiceText;
            StartCoroutine(SendPrompt(fullChoiceText, false));
        }
        else
        {
            resultText.text = $"That choice is invalid: {reason}";
            EnableAllChoiceButtons();
            waitingForAI = false;
        }
    }

    // ------ Fast, C#-only stats reporting with NO LLM call ------
    void OnStatsButtonClicked()
    {
        if (waitingForAI) return;
        statsMode = true;
        DisableAllChoiceButtons();
        sendButton.interactable = false;
        statsButton.interactable = false;
        continueButton.gameObject.SetActive(true);
        inputField.interactable = false;

        // Direct C# stat display in required format
        resultText.text = $"Money: (${money}), Reputation: ({reputation}), Risk: ({risk}%)";
    }
    // -----------------------------------------------------------

    void OnContinueButtonClicked()
    {
        statsMode = false;
        resultText.text = "Thinking...";
        continueButton.gameObject.SetActive(false);

        if (isFirstPrompt)
            SendIntroPrompt();
        else if (!string.IsNullOrEmpty(lastPlayerPrompt))
            StartCoroutine(SendPrompt(lastPlayerPrompt, false));
        else
            resultText.text = "Unable to continue. Please restart the game.";
    }

    IEnumerator SendPrompt(string playerPrompt, bool isGameStart)
    {
        waitingForAI = true;

        string statString = $"Current stats: Money: ${money}, Reputation: {reputation}, Risk: {risk}%";
        string fullPrompt;

        if (statsMode)
        {
            // This branch should not trigger, since C# now instantly reports stats!
            // But if you ever want AI reporting back, restore previous code here.
            fullPrompt = introContext + "\n" + statFormat + "\n" + statString;
        }
        else if (isGameStart || isFirstPrompt)
        {
            fullPrompt = introContext + "\n" + statString + "\nPlayer action: " + playerPrompt + "\n" + turnFormat;
            isFirstPrompt = false;
        }
        else
        {
            fullPrompt = turnFormat + "\n" + statString + "\nPlayer action: " + playerPrompt;
        }

        Debug.Log("==== LLM PROMPT SENT ====\n" + fullPrompt);

        OllamaRequest req = new OllamaRequest
        {
            model = model,
            prompt = fullPrompt,
            stream = false
        };
        string jsonBody = JsonUtility.ToJson(req);

        using (UnityWebRequest www = new UnityWebRequest(ollamaURL, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                resultText.text = "Error: " + www.error + "\n" + www.downloadHandler.text;
                Debug.LogError("Network error: " + www.error + "\n" + www.downloadHandler.text);
                DisableAllChoiceButtons();
                statsButton.interactable = false;
                continueButton.gameObject.SetActive(false);
                waitingForAI = false;
            }
            else
            {
                string json = www.downloadHandler.text;
                string reply = ExtractResponse(json);

                Debug.Log("AI RAW REPLY: \n" + reply);

                if (statsMode)
                {
                    // Should not be used, see comment above; stats are now shown directly.
                }
                else
                {
                    ShowChoicesFromNumberedReply(reply, out string summary);
                    resultText.text = summary ?? "Choose an option below.";

                    EnableAllChoiceButtons();
                    sendButton.interactable = true;
                    inputField.interactable = true;
                    statsButton.interactable = true;
                    continueButton.gameObject.SetActive(false);
                }

                waitingForAI = false;
            }
        }
    }

    void UpdateStatDisplay()
    {
        if (moneyText != null) moneyText.text = $"Money: ${money}";
        if (reputationText != null) reputationText.text = $"Reputation: {reputation}";
        if (riskText != null) riskText.text = $"Risk: {risk}%";
    }

    bool CanApplyChoiceEffect(string choiceText, out string failReason)
    {
        failReason = "";
        int tempMoney = money, tempReputation = reputation, tempRisk = risk;
        bool valid = TryParseChoiceEffect(choiceText, out int moneyDelta, out int reputationDelta, out int riskDelta);

        if (!valid)
        {
            failReason = "Couldn't parse stat effect properly.";
            return false;
        }

        if (tempMoney + moneyDelta < 0)
        {
            failReason = "You don't have enough money for that.";
            return false;
        }
        if (tempReputation + reputationDelta < 0)
        {
            failReason = "Your reputation can't go below zero.";
            return false;
        }
        if (tempRisk + riskDelta < 0)
        {
            failReason = "Your risk can't go below zero.";
            return false;
        }
        return true;
    }

    bool TryParseChoiceEffect(string choiceText, out int moneyDelta, out int reputationDelta, out int riskDelta)
    {
        // Expect input like: (Money [+100], Reputation [-2], Risk [+5%])
        moneyDelta = 0;
        reputationDelta = 0;
        riskDelta = 0;

        Match parenMatch = Regex.Match(choiceText, @"$$([^)]*)$$");
        if (!parenMatch.Success) return false;
        string effectStr = parenMatch.Groups[1].Value;

        // Strict REGEX for bracketed stats
        var moneyStrict = Regex.Match(effectStr, @"Money\s*$$\s*([+-])\$?(\d+)\s*$$", RegexOptions.IgnoreCase);
        var reputationStrict = Regex.Match(effectStr, @"Reputation\s*$$\s*([+-])(\d+)\s*$$", RegexOptions.IgnoreCase);
        var riskStrict = Regex.Match(effectStr, @"Risk\s*$$\s*([+-])(\d+)%\s*$$", RegexOptions.IgnoreCase);

        bool strictSuccess = moneyStrict.Success && reputationStrict.Success && riskStrict.Success;
        if (strictSuccess)
        {
            moneyDelta = (moneyStrict.Groups[1].Value == "-" ? -1 : 1) * int.Parse(moneyStrict.Groups[2].Value);
            reputationDelta = (reputationStrict.Groups[1].Value == "-" ? -1 : 1) * int.Parse(reputationStrict.Groups[2].Value);
            riskDelta = (riskStrict.Groups[1].Value == "-" ? -1 : 1) * int.Parse(riskStrict.Groups[2].Value);
            return true;
        }

        // Fallback (loose parsing)
        string[] parts = effectStr.Split(',').Select(p => p.Trim()).ToArray();
        foreach (var part in parts)
        {
            var m = Regex.Match(part, @"Money\s*$$\s*([+-])\$?(\d+)\s*$$", RegexOptions.IgnoreCase);
            if (m.Success) int.TryParse((m.Groups[1].Value == "-" ? "-" : "") + m.Groups[2].Value, out moneyDelta);

            var r = Regex.Match(part, @"Reputation\s*$$\s*([+-])(\d+)\s*$$", RegexOptions.IgnoreCase);
            if (r.Success) int.TryParse((r.Groups[1].Value == "-" ? "-" : "") + r.Groups[2].Value, out reputationDelta);

            var k = Regex.Match(part, @"Risk\s*$$\s*([+-])(\d+)%\s*$$", RegexOptions.IgnoreCase);
            if (k.Success) int.TryParse((k.Groups[1].Value == "-" ? "-" : "") + k.Groups[2].Value, out riskDelta);
        }
        return true;
    }

    void ApplyChoiceEffect(string choiceText)
    {
        if (TryParseChoiceEffect(choiceText, out int moneyDelta, out int reputationDelta, out int riskDelta))
        {
            money += moneyDelta;
            reputation += reputationDelta;
            risk += riskDelta;
        }
        UpdateStatDisplay();
    }

    void ShowChoicesFromNumberedReply(string aiReply, out string mainSentence)
    {
        DisableAllChoiceButtons();
        string[] lines = aiReply.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();

        mainSentence = null;
        var choiceLines = new string[3] { "...", "...", "..." };
        int iChoice = 0;
        for (int i = 0; i < lines.Length; ++i)
        {
            if (Regex.IsMatch(lines[i], @"^\d+[\.\)]"))
            {
                if (iChoice < 3)
                {
                    string cleanText = Regex.Replace(lines[i], @"^\d+[\.\)]\s*", "");
                    choiceLines[iChoice] = $"{iChoice + 1}. {cleanText}";
                    iChoice++;
                }
            }
            else if (mainSentence == null)
            {
                mainSentence = lines[i];
            }
        }
        choice1Text.text = choiceLines[0];
        choice2Text.text = choiceLines[1];
        choice3Text.text = choiceLines[2];
        choice1Button.interactable = choiceLines[0] != "...";
        choice2Button.interactable = choiceLines[1] != "...";
        choice3Button.interactable = choiceLines[2] != "...";
    }

    string ExtractResponse(string json)
    {
        try
        {
            var search = "\"response\":\"";
            int start = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (start == -1) return "Error: No 'response' field in JSON.";
            start += search.Length;
            int end = json.IndexOf("\",", start, StringComparison.OrdinalIgnoreCase);
            if (end == -1)
                end = json.IndexOf("\"", start, StringComparison.OrdinalIgnoreCase);
            if (end == -1) return "Error: Malformed JSON 'response' field.";
            string resp = json.Substring(start, end - start);
            resp = resp.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
            return resp;
        }
        catch (Exception ex)
        {
            Debug.LogError("ExtractResponse exception: " + ex.Message);
            return "Error: Response parse failed.";
        }
    }
}