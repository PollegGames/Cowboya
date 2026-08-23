using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GarageDoorControllerTests {
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown() {
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void Endpoints_KeepPositiveEdgeFixedAndToggleCollider() {
        GarageDoorController door = CreateDoor(out Transform panel, out BoxCollider collider);
        float closedPositiveEdge = panel.localPosition.y + panel.localScale.y * 0.5f;

        door.SetOpenImmediate();

        Assert.AreEqual(GarageDoorState.Open, door.State);
        Assert.That(panel.localScale.y, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(panel.localPosition.y + panel.localScale.y * 0.5f,
            Is.EqualTo(closedPositiveEdge).Within(0.0001f));
        Assert.IsFalse(collider.enabled);

        door.SetClosedImmediate();

        Assert.AreEqual(GarageDoorState.Closed, door.State);
        Assert.That(panel.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(panel.localPosition.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.IsTrue(collider.enabled);
    }

    [Test]
    public void Commands_TransitionThroughAllFourStates() {
        GarageDoorController door = CreateDoor(out _, out BoxCollider collider);
        door.SetOpenImmediate();

        door.CloseDoor();
        Assert.AreEqual(GarageDoorState.Closing, door.State);
        Assert.IsFalse(collider.enabled);

        door.StepAnimation(1f);
        Assert.AreEqual(GarageDoorState.Closed, door.State);
        Assert.IsTrue(collider.enabled);

        door.OpenDoor();
        Assert.AreEqual(GarageDoorState.Opening, door.State);
        Assert.IsFalse(collider.enabled);

        door.StepAnimation(1f);
        Assert.AreEqual(GarageDoorState.Open, door.State);
        Assert.IsFalse(collider.enabled);
    }

    [Test]
    public void ReentrantCommands_DoNotRestartOrSnapAndCanReverse() {
        GarageDoorController door = CreateDoor(out Transform panel, out _);
        door.SetOpenImmediate();
        door.CloseDoor();
        door.StepAnimation(0.2f);
        float partiallyClosedScale = panel.localScale.y;

        door.CloseDoor();
        Assert.That(panel.localScale.y, Is.EqualTo(partiallyClosedScale).Within(0.0001f));

        door.OpenDoor();
        Assert.AreEqual(GarageDoorState.Opening, door.State);
        Assert.That(panel.localScale.y, Is.EqualTo(partiallyClosedScale).Within(0.0001f));

        door.StepAnimation(0.1f);
        Assert.Less(panel.localScale.y, partiallyClosedScale);
    }

    [Test]
    public void CompletionEvents_FireOnlyAtAnimationEndpoints() {
        GarageDoorController door = CreateDoor(out _, out _);
        int openedCount = 0;
        int closedCount = 0;
        door.OnOpened += () => openedCount++;
        door.OnClosed += () => closedCount++;

        door.SetOpenImmediate();
        door.CloseDoor();
        door.StepAnimation(0.3f);
        Assert.AreEqual(0, closedCount);
        door.StepAnimation(0.3f);
        Assert.AreEqual(1, closedCount);

        door.StepAnimation(1f);
        door.CloseDoor();
        Assert.AreEqual(1, closedCount);

        door.OpenDoor();
        door.StepAnimation(0.6f);
        Assert.AreEqual(1, openedCount);
    }

    [Test]
    public void GaragePrefab_IsWiredToPanelAndBlockingCollider() {
        const string path = "Assets/Resources/Prefabs/Map/Basic/Machines/GarageCubes.prefab";
        string scriptGuid = AssetDatabase.AssetPathToGUID(
            "Assets/Scripts/Factory/Garage/GarageDoorController.cs");
        string prefabYaml = File.ReadAllText(path);

        StringAssert.Contains($"guid: {scriptGuid}", prefabYaml);
        StringAssert.Contains("panelTransform: {fileID: 1655509297258182914}", prefabYaml);
        StringAssert.Contains("blockingCollider: {fileID: 604489823012121161}", prefabYaml);
        StringAssert.Contains("scaleAxis: 2", prefabYaml);
        StringAssert.Contains("anchoredEdge: 1", prefabYaml);
        StringAssert.Contains("initialState: 0", prefabYaml);
    }

    private GarageDoorController CreateDoor(out Transform panel, out BoxCollider collider) {
        GameObject root = CreateObject("Garage");
        root.SetActive(false);
        GameObject panelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        createdObjects.Add(panelObject);
        panelObject.name = "Panel";
        panelObject.transform.SetParent(root.transform, false);
        panel = panelObject.transform;
        collider = panelObject.GetComponent<BoxCollider>();

        GarageDoorController door = root.AddComponent<GarageDoorController>();
        door.enabled = false;
        SetPrivateField(door, "panelTransform", panel);
        SetPrivateField(door, "blockingCollider", collider);
        SetPrivateField(door, "scaleAxis", GarageDoorScaleAxis.LocalY);
        SetPrivateField(door, "anchoredEdge", GarageDoorAnchoredEdge.Positive);
        SetPrivateField(door, "openScaleMultiplier", 0.05f);
        SetPrivateField(door, "initialState", GarageDoorState.Closed);
        SetPrivateField(door, "closeDuration", 0.6f);
        SetPrivateField(door, "openDuration", 0.6f);
        SetPrivateField(door, "initialized", false);
        InvokePrivate(door, "Initialize");
        return door;
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName) {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Expected method '{methodName}'.");
        method.Invoke(target, null);
    }
}
