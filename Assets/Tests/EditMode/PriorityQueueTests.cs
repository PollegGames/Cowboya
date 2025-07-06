using NUnit.Framework;

public class PriorityQueueTests
{
    [Test]
    public void EnqueueDequeue_MaintainsSortedOrder()
    {
        var pq = new PriorityQueue<int>((a, b) => a.CompareTo(b));
        pq.Enqueue(5);
        pq.Enqueue(1);
        pq.Enqueue(3);

        Assert.AreEqual(1, pq.Dequeue());
        Assert.AreEqual(3, pq.Dequeue());
        Assert.AreEqual(5, pq.Dequeue());
    }
}
