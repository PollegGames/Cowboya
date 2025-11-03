using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;

public class HintManagerTests
{
    private class DummyInput : MonoBehaviour, IPlayerInput
    {
        public Vector2 Movement { get; set; }
        public Vector2 Look { get; set; }
        public bool JumpPressed { get; set; }
        public bool PrimaryAttack { get; set; }
        public bool LeftGrabDown { get; set; }
        public bool LeftGrabHeld { get; set; }
        public bool LeftGrabUp { get; set; }
        public bool RightGrabDown { get; set; }
        public bool RightGrabHeld { get; set; }
        public bool RightGrabUp { get; set; }
    }

    private HintManager hintManager;
    private DummyInput input;
    private MessageService messageService;
    private Label label;
    private MethodInfo updateMethod;
    private MethodInfo healthMethod;

    [SetUp]
    public void SetUp()
    {
        if (MessageService.Instance != null)
            Object.DestroyImmediate(MessageService.Instance.gameObject);

        var serviceObj = new GameObject("MessageService");
        messageService = serviceObj.AddComponent<MessageService>();
        var root = new VisualElement();
        label = new Label();
        label.name = "GameMessageLabel";
        label.style.display = DisplayStyle.None;
        root.Add(label);
        messageService.Initialize(root);

        var hintObj = new GameObject("HintManager");
        input = hintObj.AddComponent<DummyInput>();
        hintManager = hintObj.AddComponent<HintManager>();
        typeof(HintManager).GetField("inputSource", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(hintManager, input);

        updateMethod = typeof(HintManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        healthMethod = typeof(HintManager).GetMethod("HandleHealthChanged", BindingFlags.NonPublic | BindingFlags.Instance);

        typeof(HintManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(hintManager, null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(hintManager.gameObject);
        Object.DestroyImmediate(messageService.gameObject);
    }

    [Test]
    public void Movement_ShowsHintOnce()
    {
        input.Movement = Vector2.right;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(GameMessages.Hints.MovementEnergy.Text, label.text);

        label.style.display = DisplayStyle.None;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(DisplayStyle.None, label.style.display);
    }

    [Test]
    public void PrimaryAttack_ShowsHintOnce()
    {
        input.PrimaryAttack = true;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(GameMessages.Hints.TargetAttack.Text, label.text);

        label.style.display = DisplayStyle.None;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(DisplayStyle.None, label.style.display);
    }

    [Test]
    public void Grab_ShowsHintOnce()
    {
        input.LeftGrabDown = true;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(GameMessages.Hints.InteractGrab.Text, label.text);

        label.style.display = DisplayStyle.None;
        input.LeftGrabDown = false;
        input.LeftGrabDown = true;
        updateMethod.Invoke(hintManager, null);
        Assert.AreEqual(DisplayStyle.None, label.style.display);
    }

    [Test]
    public void Damage_ShowsHintOnce()
    {
        healthMethod.Invoke(hintManager, new object[] { -10f });
        Assert.AreEqual(GameMessages.Hints.Health.Text, label.text);

        label.style.display = DisplayStyle.None;
        healthMethod.Invoke(hintManager, new object[] { -5f });
        Assert.AreEqual(DisplayStyle.None, label.style.display);
    }
}
