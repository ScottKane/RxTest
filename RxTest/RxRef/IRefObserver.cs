namespace RxTest;

internal interface IRefObserver<T> : IObserver<T>
{
    void OnNext(ref T value);
}