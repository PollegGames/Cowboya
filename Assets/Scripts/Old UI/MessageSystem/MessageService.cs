using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MessageService : MonoBehaviour
{
    public static MessageService Instance { get; private set; }

    private Label messageLabel;
    private VisualElement root;
    private readonly HashSet<GameMessage> displayedHints = new();

    public bool IsNotDisplaying => messageLabel == null || messageLabel.style.display == DisplayStyle.None;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Initialize(VisualElement rootElement)
    {
        root = rootElement;
        messageLabel = root.Q<Label>("GameMessageLabel");
    }

    /// <summary>
    /// Displays a game message for the specified duration.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">How long to display the message.</param>
    public void ShowMessage(GameMessage message, float duration = 6f)
    {
        if (messageLabel == null) return;

        string speakerPrefix = message.Speaker switch
        {
            MessageSpeaker.DrHex => "Dr Hex: ",
            MessageSpeaker.Player => "Me: ",
            MessageSpeaker.Narrator => "",
            _ => ""
        };

        messageLabel.text = speakerPrefix + message.Text;
        messageLabel.style.display = DisplayStyle.Flex;

        CancelInvoke(nameof(HideMessage));
        Invoke(nameof(HideMessage), duration);
    }

    /// <summary>
    /// Hides any currently displayed message immediately.
    /// </summary>
    public void HideMessage()
    {
        CancelInvoke(nameof(HideMessage));
        if (messageLabel != null)
            messageLabel.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Displays a hint message if it hasn't been shown before or if forced.
    /// </summary>
    /// <param name="id">The hint message to display.</param>
    /// <param name="duration">Optional duration override.</param>
    /// <param name="force">Show even if previously displayed.</param>
    public void ShowHint(GameMessage id, float? duration = null, bool force = false)
    {
        if (!force && displayedHints.Contains(id))
            return;

        displayedHints.Add(id);
        float showDuration = duration ?? Random.Range(6f, 8f);
        ShowMessage(id, showDuration);
    }
}
