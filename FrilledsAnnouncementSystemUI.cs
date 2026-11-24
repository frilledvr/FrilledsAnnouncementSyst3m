using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

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

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
    public TMP_Text tmpText;
    public TMP_Text tmpSenderText;
#endif

    [Header("Username Prefix")]
    public bool showUsernamePrefix = true;
    public string usernameSeparator = ": ";

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
            StartCoroutine(Poll());
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
                    data = JsonUtility.FromJson<AnnouncementResponse>(
                        req.downloadHandler.text
                    );
                }
                catch
                {
                    // Ignore invalid response data.
                }

                if (data != null && data.items != null && data.items.Length > 0)
                {
                    AnnouncementItem item = data.items[0];

                    if (firstPoll)
                    {
                        lastSeenCount = item.createdAt;
                        latest = item.message;
                        firstPoll = false;
                    }
                    else if (item.createdAt != lastSeenCount ||
                             item.message != latest)
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

            yield return new WaitForSeconds(
                Mathf.Max(2f, pollSeconds)
            );
        }
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

        string safeSender = string.IsNullOrWhiteSpace(sender)
            ? "Announcement"
            : sender.Trim();

        string formattedMessage = showUsernamePrefix
            ? safeSender + usernameSeparator + message
            : message;

        // Prevent separate sender labels from duplicating the prefix.
        SetSender("");

        if (legacyText)
            legacyText.text = "";

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        if (tmpText)
            tmpText.text = "";
#endif

        if (panelCanvasGroup)
            panelCanvasGroup.alpha = 0;

        typingCoroutine = StartCoroutine(
            TypeMessage(formattedMessage)
        );
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

        fadeCoroutine = StartCoroutine(Fade(1f));

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

            if (typingSound && typingAudio &&
                !char.IsWhiteSpace(message[i]))
            {
                typingAudio.PlayOneShot(
                    typingSound,
                    typingSoundVolume
                );
            }

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
        float height = 100f;

#if TMP_PRESENT || TEXTMESHPRO_PRESENT
        if (tmpText)
        {
            tmpText.ForceMeshUpdate();

            Vector2 preferred = tmpText.GetPreferredValues(
                tmpText.text,
                maxWidth - padding.x,
                10000f
            );

            width = Mathf.Clamp(
                preferred.x + padding.x,
                minWidth,
                maxWidth
            );

            height = preferred.y + padding.y;
        }
        else
#endif
        if (legacyText)
        {
            Vector2 generationSize = new Vector2(
                maxWidth - padding.x,
                10000f
            );

            legacyText.cachedTextGenerator.Populate(
                legacyText.text,
                legacyText.GetGenerationSettings(generationSize)
            );

            float preferredWidth =
                legacyText.cachedTextGenerator.GetPreferredWidth(
                    legacyText.text,
                    legacyText.GetGenerationSettings(generationSize)
                );

            width = Mathf.Clamp(
                preferredWidth + padding.x,
                minWidth,
                maxWidth
            );

            height = legacyText.preferredHeight + padding.y;
        }

        panel.sizeDelta = new Vector2(width, height);
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displaySeconds);

        yield return Fade(0f);

        if (panel)
            panel.gameObject.SetActive(false);
    }

    IEnumerator Fade(float target)
    {
        if (!panelCanvasGroup)
            yield break;

        float start = panelCanvasGroup.alpha;
        float time = 0f;

        if (fadeDuration <= 0f)
        {
            panelCanvasGroup.alpha = target;
            yield break;
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;

            panelCanvasGroup.alpha = Mathf.Lerp(
                start,
                target,
                time / fadeDuration
            );

            yield return null;
        }

        panelCanvasGroup.alpha = target;
    }

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
