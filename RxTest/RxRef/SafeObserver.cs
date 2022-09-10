using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;

namespace RxTest;

internal abstract class SafeObserver<TSource> : ISafeObserver<TSource>
{
    private sealed class WrappingSafeObserver : SafeObserver<TSource>
    {
        private readonly IObserver<TSource> _observer;

        public WrappingSafeObserver(IObserver<TSource> observer) => _observer = observer;

        public override void OnNext(TSource value)
        {
        }

        public override void OnNext(ref TSource value)
        {
            var noError = false;
            try
            {
                _observer.OnNext(value);
                noError = true;
            }
            finally
            {
                if (!noError) Dispose();
            }
        }

        public override void OnError(Exception error)
        {
            using (this) _observer.OnError(error);
        }

        public override void OnCompleted()
        {
            using (this) _observer.OnCompleted();
        }
    }

    public static ISafeObserver<TSource> Wrap(IObserver<TSource> observer)
    {
        if (observer is RefAnonymousObserver<TSource> a) return a.MakeSafe();

        return new WrappingSafeObserver(observer);
    }

    public static ISafeObserver<TSource> Wrap(IRefObserver<TSource> observer)
    {
        if (observer is RefAnonymousObserver<TSource> a) return a.MakeSafe();

        return new WrappingSafeObserver(observer);
    }

    private SingleAssignmentDisposableValue _disposable;

    public abstract void OnNext(ref TSource value);
    public abstract void OnNext(TSource value);

    public abstract void OnError(Exception error);

    public abstract void OnCompleted();

    public void SetResource(IDisposable resource) => _disposable.Disposable = resource;

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) _disposable.Dispose();
    }

    private struct SingleAssignmentDisposableValue
    {
        private IDisposable? _current;

        public IDisposable? Disposable
        {
            set
            {
                var result = Disposables.TrySetSingle(ref _current, value);

                if (result == TrySetSingleResult.AlreadyAssigned)
                    throw new InvalidOperationException("Disposable is already assigned");
            }
        }

        public void Dispose() => Disposables.Dispose(ref _current);


        private static class Disposables
        {
            internal static TrySetSingleResult TrySetSingle([NotNullIfNotNull("value")] ref IDisposable? fieldRef,
                IDisposable? value)
            {
                var old = Interlocked.CompareExchange(ref fieldRef, value, null);
                if (old == null) return TrySetSingleResult.Success;

                if (!Equals(old, BooleanDisposable.True)) return TrySetSingleResult.AlreadyAssigned;

                value?.Dispose();
                return TrySetSingleResult.Disposed;
            }

            internal static void Dispose([NotNullIfNotNull("fieldRef")] ref IDisposable? fieldRef)
            {
                var old = Interlocked.Exchange(ref fieldRef, BooleanDisposable.True);

                if (!Equals(old, BooleanDisposable.True)) old?.Dispose();
            }
        }

        private sealed class BooleanDisposable : ICancelable
        {
            internal static readonly BooleanDisposable True = new(true);

            private volatile bool _isDisposed;
            
            private BooleanDisposable(bool isDisposed) => _isDisposed = isDisposed;

            public bool IsDisposed => _isDisposed;

            public void Dispose() => _isDisposed = true;
        }

        private enum TrySetSingleResult
        {
            Success,
            AlreadyAssigned,
            Disposed
        }
    }
}