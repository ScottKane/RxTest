namespace RxTest;

internal static class ObservableExtensions
{
    public static IRefObservable<T> Where<T>(this IRefObservable<T> source, Func<T, bool> predicate) =>
        new WhereOperator<T>(source, predicate);

    public static IDisposable Subscribe<T>(this IRefObservable<T> source, RefAction<T> onNext)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (onNext == null) throw new ArgumentNullException(nameof(onNext));

        return source.Subscribe(new RefAnonymousObserver<T>(onNext, Stubs.Throw, Stubs.Nop));
    }

    private class WhereOperator<T> : IRefObservable<T>
    {
        private readonly IRefObservable<T> _source;
        private readonly Func<T, bool>     _predicate;

        public WhereOperator(IRefObservable<T> source, Func<T, bool> predicate)
        {
            _source    = source;
            _predicate = predicate;
        }

        public IDisposable Subscribe(IRefObserver<T> observer)
        {
            var filter = new Filter<T>(observer, _predicate);
            return _source.Subscribe(filter);
        }

        private class Filter<TU> : IRefObserver<TU>
        {
            private readonly IRefObserver<TU> _observer;
            private readonly Func<TU, bool>   _predicate;

            public Filter(IRefObserver<TU> observer, Func<TU, bool> predicate)
            {
                _observer  = observer;
                _predicate = predicate;
            }

            public void OnNext(TU value)
            {
                if (_predicate(value)) _observer.OnNext(value);
            }

            public void OnNext(ref TU value)
            {
                if (_predicate(value)) _observer.OnNext(ref value);
            }

            public void OnCompleted() => _observer.OnCompleted();

            public void OnError(Exception error) => _observer.OnError(error);
        }

        public IDisposable Subscribe(IObserver<T> observer) => _source.Where(_predicate).Subscribe();
    }
}