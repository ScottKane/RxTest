namespace RxTest;

internal interface ISafeObserver<T> : IRefObserver<T>, IDisposable
{
    void SetResource(IDisposable resource);
}