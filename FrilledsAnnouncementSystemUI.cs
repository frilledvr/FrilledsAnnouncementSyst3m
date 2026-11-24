using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class FrilledsAnnouncementSystemUI : MonoBehaviour
{
    [Header("UI")]
    public RectTransform panel;
    public Text legacyText;
    public Text senderText;
    public Image panelImage;
    public CanvasGroup panelCanvasGroup;
    [Header("Poll UI")]
    public Text pollText;
    public float pollRefreshSeconds = 5f;
    [Header("Poll Input")]
    public float leftThreshold = -0.65f;
    private string activePollId = "";
    private bool votedPoll = false;
    private InputDevice leftDevice;
    private bool previousClick = false;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
    public TMP_Text tmpText;
    public TMP_Text tmpSenderText;
#endif

    [Header("Panel Sizing")]
    public Vector2 padding = new Vector2(40, 30);
    public float minWidth = 300;
    public float maxWidth = 900;

    [Header("Typing")]
    public float typingSpeed = 0.035f;
    public AudioSource typingAudio;
    public AudioClip typingSound;
    public float typingSoundVolume = 0.5f;

    [Header("Fade")]
    public float fadeDuration = 0.3f;
    public float displaySeconds = 8f;

    [Header("Backend")]
    public string backendUrl = "http://localhost:3000";
    public float pollSeconds = 5f;
    public bool pollOnStart = true;

    private string latest = "";
    private long lastSeenCount = -1;
    private bool firstPoll = true;
    private Coroutine typingCoroutine;
    private Coroutine fadeCoroutine;
    private Coroutine hideCoroutine;

    void Start()
    {
        if (panelCanvasGroup)
            panelCanvasGroup.alpha = 0;

        if (panel)
            panel.gameObject.SetActive(false);

        if (pollOnStart)
        { StartCoroutine(Poll()); StartCoroutine(PollActivePoll()); }
    }

    IEnumerator Poll()
    {
        while (true)
        {
            UnityWebRequest req = UnityWebRequest.Get(
                backendUrl.TrimEnd('/') + "/announcements"
            );

            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                AnnouncementResponse data = null;

                try
                {
                    data = JsonUtility.FromJson<AnnouncementResponse>(req.downloadHandler.text);
                }
                catch { }

                if (data != null && data.items != null && data.items.Length > 0)
                {
                    var item = data.items[0];

                    // Don't replay the most recent announcement when the game starts.
                    if (firstPoll)
                    {
                        lastSeenCount = item.createdAt;
                        latest = item.message;
                        firstPoll = false;
                    }
                    else if (item.createdAt != lastSeenCount && item.message != latest)
                    {
                        lastSeenCount = item.createdAt;
                        latest = item.message;
                        DisplayAnnouncement(item.message, item.sender);
                    }
                }
                else
                {
                    firstPoll = false;
                }
            }

            req.Dispose();
            yield return new WaitForSeconds(Mathf.Max(2, pollSeconds));
        }
    }

    IEnumerator PollActivePoll()
    {
        while (true)
        {
            UnityWebRequest req = UnityWebRequest.Get(backendUrl.TrimEnd('/') + "/poll");
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                PollResponse data = null;
                try { data = JsonUtility.FromJson<PollResponse>(req.downloadHandler.text); } catch { }
                if (data != null && data.poll != null)
                {
                    if (pollText && !pollText.gameObject.activeSelf) pollText.gameObject.SetActive(true);
                    if (activePollId != data.poll.id) { activePollId = data.poll.id; votedPoll = false; }
                    if (pollText) pollText.text = "POLL: " + data.poll.question + "\nPush LEFT + click left joystick to vote for: " + data.poll.options[0] + "\n" + data.poll.options[1] + "\nVotes: " + data.poll.votes[0] + " / " + data.poll.votes[1];
                    CheckPollInput(data.poll);
                }
                else { if (pollText) { pollText.text = ""; if (pollText.gameObject.activeSelf) pollText.gameObject.SetActive(false); } activePollId = ""; votedPoll = false; }
            }
            req.Dispose();
            yield return new WaitForSeconds(Mathf.Max(2f, pollRefreshSeconds));
        }
    }

    void CheckPollInput(PollData poll)
    {
        if (votedPoll || poll.options == null || poll.options.Length < 1) return;
        if (!leftDevice.isValid) leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        Vector2 axis; bool click;
        bool gotAxis = leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis);
        bool gotClick = leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out click);
        if (gotAxis && gotClick && axis.x < leftThreshold && click && !previousClick)
            StartCoroutine(SendVote(0));
        previousClick = click;
    }

    IEnumerator SendVote(int choice)
    {
        string json = "{\"choice\":" + choice + "}";
        UnityWebRequest req = new UnityWebRequest(backendUrl.TrimEnd('/') + "/vote", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success) { votedPoll = true; if (pollText) pollText.text += "\nVOTE SUBMITTED"; }
        else if (pollText) pollText.text += "\nVote failed: " + req.error;
        req.Dispose();
    }

    void DisplayAnnouncement(string message, string sender)
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        if (panel)
            panel.gameObject.SetActive(true);

        SetSender(sender);

        if (legacyText)
            legacyText.text = "";

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        if (tmpText)
            tmpText.text = "";
#endif

        if (panelCanvasGroup)
            panelCanvasGroup.alpha = 0;

        typingCoroutine = StartCoroutine(TypeMessage(message));
    }

    void SetSender(string sender)
    {
        if (senderText)
            senderText.text = sender;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        if (tmpSenderText)
            tmpSenderText.text = sender;
#endif
    }

    IEnumerator TypeMessage(string message)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(Fade(1));
        string current = "";

        for (int i = 0; i < message.Length; i++)
        {
            current += message[i];

            if (legacyText)
                legacyText.text = current;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
            if (tmpText)
                tmpText.text = current;
#endif

            ResizePanel();

            if (typingSound && typingAudio && !char.IsWhiteSpace(message[i]))
                typingAudio.PlayOneShot(typingSound, typingSoundVolume);

            yield return new WaitForSeconds(typingSpeed);
        }

        ResizePanel();
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    void ResizePanel()
    {
        if (!panel)
            return;

        float width = minWidth;
        float height = 100;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        if (tmpText)
        {
            tmpText.ForceMeshUpdate();
            Vector2 preferred = tmpText.GetPreferredValues(tmpText.text, maxWidth - padding.x, 10000);
            width = Mathf.Clamp(preferred.x + padding.x, minWidth, maxWidth);
            height = preferred.y + padding.y;
        }
        else
#endif
        if (legacyText)
        {
            Vector2 generationSize = new Vector2(maxWidth - padding.x, 10000f);
            legacyText.cachedTextGenerator.Populate(
                legacyText.text,
                legacyText.GetGenerationSettings(generationSize)
            );

            float preferredWidth = legacyText.cachedTextGenerator.GetPreferredWidth(
                legacyText.text,
                legacyText.GetGenerationSettings(generationSize)
            );

            width = Mathf.Clamp(preferredWidth + padding.x, minWidth, maxWidth);
            height = legacyText.preferredHeight + padding.y;
        }

        panel.sizeDelta = new Vector2(width, height);
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displaySeconds);
        yield return Fade(0);

        if (panel)
            panel.gameObject.SetActive(false);
    }

    IEnumerator Fade(float target)
    {
        if (!panelCanvasGroup)
            yield break;

        float start = panelCanvasGroup.alpha;
        float time = 0;

        if (fadeDuration <= 0)
        {
            panelCanvasGroup.alpha = target;
            yield break;
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(start, target, time / fadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = target;
    }

    [System.Serializable] class PollResponse { public PollData poll; }
    [System.Serializable] class PollData { public string id; public string question; public string[] options; public int[] votes; }

    [System.Serializable]
    class AnnouncementResponse
    {
        public AnnouncementItem[] items;
    }

    [System.Serializable]
    class AnnouncementItem
    {
        public string message;
        public string sender;
        public long createdAt;
    }
}
