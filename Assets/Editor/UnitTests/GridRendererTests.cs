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
        var cells = new Dictionary<Vector2, Cell>
        {
            { Vector2.zero, new Cell(Vector2.zero) }
        };

        var result = _renderer.Render(cells, Vector2.one, Vector3.zero, new GameObject());

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.ContainsKey(Vector2.zero));
    }
}
