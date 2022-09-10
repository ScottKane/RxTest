using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;

namespace RxTest;

[SuppressMessage("ReSharper", "CognitiveComplexity")]
internal sealed class RefSubject<T> : RefSubjectBase<T>
{
    #region Fields

    private SubjectDisposable[] _observers;
    private Exception?          _exception;

    // ReSharper disable once UseArrayEmptyMethod - DO NOT USE Array.Empty here
    private static readonly SubjectDisposable[] Terminated = new SubjectDisposable[0];

    // ReSharper disable once UseArrayEmptyMethod - DO NOT USE Array.Empty here
    private static readonly SubjectDisposable[] Disposed = new SubjectDisposable[0];

    #endregion

    #region Constructors

    public RefSubject() => _observers = Array.Empty<SubjectDisposable>();

    #endregion

    #region Properties

    public override bool HasObservers => Volatile.Read(ref _observers).Length != 0;

    public override bool IsDisposed => Volatile.Read(ref _observers) == Disposed;

    #endregion

    #region Methods

    #region IObserver<T> implementation

    private static void ThrowDisposed() => throw new ObjectDisposedException(string.Empty);

    public override void OnCompleted()
    {
        for (;;)
        {
            var observers = Volatile.Read(ref _observers);

            if (observers == Disposed)
            {
                _exception = null;
                ThrowDisposed();
                break;
            }

            if (observers == Terminated) break;

            if (Interlocked.CompareExchange(ref _observers, Terminated, observers) == observers)
            {
                foreach (var observer in observers) observer.Observer?.OnCompleted();

                break;
            }
        }
    }

    public override void OnError(Exception error)
    {
        if (error == null) throw new ArgumentNullException(nameof(error));

        for (;;)
        {
            var observers = Volatile.Read(ref _observers);

            if (observers == Disposed)
            {
                _exception = null;
                ThrowDisposed();
                break;
            }

            if (observers == Terminated) break;

            _exception = error;

            if (Interlocked.CompareExchange(ref _observers, Terminated, observers) == observers)
            {
                foreach (var observer in observers) observer.Observer?.OnError(error);

                break;
            }
        }
    }

    public override void OnNext(ref T value)
    {
        var observers = Volatile.Read(ref _observers);

        if (observers == Disposed)
        {
            _exception = null;
            ThrowDisposed();
            return;
        }

        foreach (var observer in observers) observer.RefObserver?.OnNext(ref value);
    }

    public override void OnNext(T value)
    {
        var observers = Volatile.Read(ref _observers);

        if (observers == Disposed)
        {
            _exception = null;
            ThrowDisposed();
            return;
        }

        foreach (var observer in observers) observer.Observer?.OnNext(value);
    }

    #endregion

    #region IObservable<T> implementation

    public override IDisposable Subscribe(IRefObserver<T> observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));

        var disposable = default(SubjectDisposable);
        for (;;)
        {
            var observers = Volatile.Read(ref _observers);

            if (observers == Disposed)
            {
                _exception = null;
                ThrowDisposed();

                break;
            }

            if (observers == Terminated)
            {
                var ex = _exception;

                if (ex != null)
                    observer.OnError(ex);
                else
                    observer.OnCompleted();

                break;
            }

            disposable ??= new SubjectDisposable(this, observer);

            var n = observers.Length;
            var b = new SubjectDisposable[n + 1];

            Array.Copy(observers, 0, b, 0, n);

            b[n] = disposable;

            if (Interlocked.CompareExchange(ref _observers, b, observers) == observers) return disposable;
        }

        return Disposable.Empty;
    }

    public override IDisposable Subscribe(IObserver<T> observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));

        var disposable = default(SubjectDisposable);
        for (;;)
        {
            var observers = Volatile.Read(ref _observers);

            if (observers == Disposed)
            {
                _exception = null;
                ThrowDisposed();

                break;
            }

            if (observers == Terminated)
            {
                var ex = _exception;

                if (ex != null)
                    observer.OnError(ex);
                else
                    observer.OnCompleted();

                break;
            }

            disposable ??= new SubjectDisposable(this, observer);

            var n = observers.Length;
            var b = new SubjectDisposable[n + 1];

            Array.Copy(observers, 0, b, 0, n);

            b[n] = disposable;

            if (Interlocked.CompareExchange(ref _observers, b, observers) == observers) return disposable;
        }

        return Disposable.Empty;
    }

    private void Unsubscribe(SubjectDisposable observer)
    {
        for (;;)
        {
            var a = Volatile.Read(ref _observers);
            var n = a.Length;

            if (n == 0) break;

            var j = Array.IndexOf(a, observer);

            if (j < 0) break;

            SubjectDisposable[] b;

            if (n == 1)
                b = Array.Empty<SubjectDisposable>();
            else
            {
                b = new SubjectDisposable[n - 1];

                Array.Copy(a, 0, b, 0, j);
                Array.Copy(a, j + 1, b, j, n - j - 1);
            }

            if (Interlocked.CompareExchange(ref _observers, b, a) == a) break;
        }
    }

    private sealed class SubjectDisposable : IDisposable
    {
        private          RefSubject<T>    _subject;
        private volatile IRefObserver<T>? _refObserver;
        private volatile IObserver<T>?    _observer;

        public SubjectDisposable(RefSubject<T> subject, IRefObserver<T> observer)
        {
            _subject     = subject;
            _refObserver = observer;
        }

        public SubjectDisposable(RefSubject<T> subject, IObserver<T> observer)
        {
            _subject  = subject;
            _observer = observer;
        }

        public IObserver<T>?    Observer    => _observer;
        public IRefObserver<T>? RefObserver => _refObserver;

        public void Dispose()
        {
            var observer = Interlocked.Exchange(ref _observer, null);
            if (observer == null) return;

            _subject.Unsubscribe(this);
            _subject = null!;
        }
    }

    #endregion

    #region IDisposable implementation

    public override void Dispose()
    {
        Interlocked.Exchange(ref _observers, Disposed);
        _exception = null;
    }

    #endregion

    #endregion
}