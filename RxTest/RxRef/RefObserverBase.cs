namespace RxTest;

internal abstract class RefObserverBase<T> : IRefObserver<T>, IDisposable
{
    private int _isStopped;

    protected RefObserverBase() => _isStopped = 0;

    public void OnNext(T value)
    {
        if (Volatile.Read(ref _isStopped) == 0) OnNextCore(value);
    }

    public void OnNext(ref T value)
    {
        if (Volatile.Read(ref _isStopped) == 0) OnNextCore(ref value);
    }

    protected abstract void OnNextCore(T value);

    protected abstract void OnNextCore(ref T value);

    public void OnError(Exception error)
    {
        if (error == null) throw new ArgumentNullException(nameof(error));

        if (Interlocked.Exchange(ref _isStopped, 1) == 0) OnErrorCore(error);
    }

    protected abstract void OnErrorCore(Exception error);

    public void OnCompleted()
    {
        if (Interlocked.Exchange(ref _isStopped, 1) == 0) OnCompletedCore();
    }

    protected abstract void OnCompletedCore();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Volatile.Write(ref _isStopped, 1);
    }

    internal bool Fail(Exception error)
    {
        if (Interlocked.Exchange(ref _isStopped, 1) != 0) return false;
        OnErrorCore(error);
        return true;
    }
}