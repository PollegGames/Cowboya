using NUnit.Framework;
using UnityEngine;

public class CellPropertiesTests
{
    private CellProperties _props;

    [SetUp]
    public void SetUp()
    {
        _props = new CellProperties();
    }

    [Test]
    public void Properties_DefaultValues()
    {
        Assert.IsTrue(_props.HasLeftDoor);
        Assert.IsTrue(_props.HasRightDoor);
        Assert.IsFalse(_props.HasLeftDoorLocked);
        Assert.IsFalse(_props.HasRightDoorLocked);
        Assert.IsFalse(_props.HasLiftUpBlocked);
        Assert.IsFalse(_props.HasLiftDownBlocked);
        Assert.AreEqual(UsageType.Empty, _props.usageType);
        Assert.AreEqual(POIType.None, _props.poiType);
    }
}
