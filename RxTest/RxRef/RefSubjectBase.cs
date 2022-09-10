namespace RxTest;

internal abstract class RefSubjectBase<T> : IRefSubject<T>, IDisposable
{
    public abstract bool HasObservers { get; }
    public abstract bool IsDisposed   { get; }

    public abstract void Dispose();

    public abstract void OnCompleted();

    public abstract void OnError(Exception error);

    public abstract void OnNext(T value);

    public abstract void OnNext(ref T value);

    public abstract IDisposable Subscribe(IObserver<T> observer);

    public abstract IDisposable Subscribe(IRefObserver<T> observer);
}