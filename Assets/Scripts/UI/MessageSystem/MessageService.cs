using UnityEngine;
using UnityEngine.UIElements;

public class MessageService : MonoBehaviour
{
    public static MessageService Instance { get; private set; }

    private Label messageLabel;
    private VisualElement root;

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

    public void ShowMessage(GameMessage message, float duration = 3f)
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

    public void HideMessage()
    {
        if (messageLabel != null)
            messageLabel.style.display = DisplayStyle.None;
    }
}
