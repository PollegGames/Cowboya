public interface IPooledObject
{
    void OnAcquireFromPool();
    void OnReleaseToPool();
}
