using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class GridRendererTests
{
    private GridRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _renderer = new GridRenderer(new Dictionary<UsageType, GameObject>(), new Dictionary<POIType, GameObject>(), new GameObject().transform);
    }

    [Test]
    public void Render_CreatesInstances()
    {
        // TODO: Assert rendering
    }
}
