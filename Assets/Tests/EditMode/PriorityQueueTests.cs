using NUnit.Framework;
public class PriorityQueueTests
{
    [Test]
    public void EnqueueDequeue_MaintainsSortedOrder()
    {
        #if NET6_0_OR_GREATER
                var pq = new System.Collections.Generic.PriorityQueue<int, int>();
                pq.Enqueue(5, 5);
                pq.Enqueue(1, 1);
                pq.Enqueue(3, 3);
        
                Assert.AreEqual(1, pq.Dequeue());
                Assert.AreEqual(3, pq.Dequeue());
                Assert.AreEqual(5, pq.Dequeue());
        #else
                // If not using .NET 6+, you need to implement your own PriorityQueue or use a third-party library.
                Assert.Inconclusive("PriorityQueue<TElement, TPriority> is only available in .NET 6.0 or greater.");
        #endif
    }
}
