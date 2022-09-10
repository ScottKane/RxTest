namespace RxTest;

internal interface IRefSubject<T> : IRefSubject<T, T>
{
}

internal interface IRefSubject<TSource, TResult> : IRefObserver<TSource>, IRefObservable<TResult>
{
}