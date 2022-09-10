namespace RxTest;

internal sealed class RefAnonymousObserver<T> : RefObserverBase<T>
{
    private readonly Action<T>?        _onNext;
    private readonly RefAction<T>?     _refOnNext;
    private readonly Action<Exception> _onError;
    private readonly Action            _onCompleted;

    public RefAnonymousObserver(Action<T> onNext, Action<Exception> onError, Action onCompleted)
    {
        _onNext      = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onError     = onError ?? throw new ArgumentNullException(nameof(onError));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    public RefAnonymousObserver(RefAction<T> onNext, Action<Exception> onError, Action onCompleted)
    {
        _refOnNext   = onNext ?? throw new ArgumentNullException(nameof(onNext));
        _onError     = onError ?? throw new ArgumentNullException(nameof(onError));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    public RefAnonymousObserver(RefAction<T> onNext)
        : this(onNext, Stubs.Throw, Stubs.Nop)
    {
    }

    public RefAnonymousObserver(RefAction<T> onNext, Action<Exception> onError)
        : this(onNext, onError, Stubs.Nop)
    {
    }

    public RefAnonymousObserver(RefAction<T> onNext, Action onCompleted)
        : this(onNext, Stubs.Throw, onCompleted)
    {
    }

    protected override void OnNextCore(T value) => _onNext?.Invoke(value);

    protected override void OnNextCore(ref T value) => _refOnNext?.Invoke(ref value);

    protected override void OnErrorCore(Exception error) => _onError(error);

    protected override void OnCompletedCore() => _onCompleted();

    internal ISafeObserver<T> MakeSafe() => new AnonymousSafeObserver<T>(_refOnNext!, _onError, _onCompleted);

    private sealed class AnonymousSafeObserver<TU> : SafeObserver<TU>
    {
        private readonly RefAction<TU>      _onNext;
        private readonly Action<Exception> _onError;
        private readonly Action            _onCompleted;

        private int _isStopped;

        public AnonymousSafeObserver(RefAction<TU> onNext, Action<Exception> onError, Action onCompleted)
        {
            _onNext      = onNext;
            _onError     = onError;
            _onCompleted = onCompleted;
        }

        public override void OnNext(TU value)
        {
        }

        public override void OnNext(ref TU value)
        {
            if (_isStopped == 0)
            {
                var noError = false;
                try
                {
                    _onNext(ref value);
                    noError = true;
                }
                finally
                {
                    if (!noError) Dispose();
                }
            }
        }

        public override void OnError(Exception error)
        {
            if (Interlocked.Exchange(ref _isStopped, 1) == 0)
                using (this)
                    _onError(error);
        }

        public override void OnCompleted()
        {
            if (Interlocked.Exchange(ref _isStopped, 1) == 0)
                using (this)
                    _onCompleted();
        }
    }
}