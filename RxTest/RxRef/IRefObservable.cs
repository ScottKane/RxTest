namespace RxTest;

internal interface IRefObservable<T> : IObservable<T>
{
    IDisposable Subscribe(IRefObserver<T> observer);
}