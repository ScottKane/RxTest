using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.PlatformServices;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RxTest;

public delegate void RefAction<T>(ref T item);

internal interface IRefSubject<T> : IRefSubject<T, T>
{
}

internal interface IRefSubject<TSource, TResult> : IRefObserver<TSource>, IRefObservable<TResult>
{
}

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

internal sealed class RefSubject<T> : RefSubjectBase<T>
{
    #region Fields

    private                 SubjectDisposable[] _observers;
    private                 Exception?          _exception;
    // ReSharper disable once UseArrayEmptyMethod - DO NOT USE Array.Empty here
    private static readonly SubjectDisposable[] Terminated = new SubjectDisposable[0];
    // ReSharper disable once UseArrayEmptyMethod - DO NOT USE Array.Empty here
    private static readonly SubjectDisposable[] Disposed   = new SubjectDisposable[0];

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

internal class WhereOperator<T> : IRefObservable<T>
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

    private class Filter<T> : IRefObserver<T>
    {
        private readonly IRefObserver<T> _observer;
        private readonly Func<T, bool>   _predicate;

        public Filter(IRefObserver<T> observer, Func<T, bool> predicate)
        {
            _observer  = observer;
            _predicate = predicate;
        }

        public void OnNext(T value)
        {
            if (_predicate(value)) _observer.OnNext(value);
        }

        public void OnNext(ref T value)
        {
            if (_predicate(value)) _observer.OnNext(ref value);
        }

        public void OnCompleted() => _observer.OnCompleted();

        public void OnError(Exception error) => _observer.OnError(error);
    }

    public IDisposable Subscribe(IObserver<T> observer) => _source.Where(_predicate).Subscribe();
}

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
}

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
}

internal sealed class AnonymousSafeObserver<T> : SafeObserver<T>
{
    private readonly RefAction<T>      _onNext;
    private readonly Action<Exception> _onError;
    private readonly Action            _onCompleted;

    private int _isStopped;

    public AnonymousSafeObserver(RefAction<T> onNext, Action<Exception> onError, Action onCompleted)
    {
        _onNext      = onNext;
        _onError     = onError;
        _onCompleted = onCompleted;
    }

    public override void OnNext(T value)
    {
    }

    public override void OnNext(ref T value)
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
}

internal static class Disposables
{
    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        private EmptyDisposable()
        {
        }
        
        public void Dispose()
        {
            // no op
        }
    }
    
    public static IDisposable Empty => EmptyDisposable.Instance;
    
    public static IDisposable Create(Action dispose)
    {
        if (dispose == null) throw new ArgumentNullException(nameof(dispose));

        return new AnonymousDisposable(dispose);
    }
    
    public static IDisposable Create<TState>(TState state, Action<TState> dispose)
    {
        if (dispose == null) throw new ArgumentNullException(nameof(dispose));

        return new AnonymousDisposable<TState>(state, dispose);
    }

    internal static IDisposable? GetValue([NotNullIfNotNull("fieldRef")] /*in*/ ref IDisposable? fieldRef)
    {
        var current = Volatile.Read(ref fieldRef);

        return current == BooleanDisposable.True
            ? null
            : current;
    }
    
    [return: NotNullIfNotNull("fieldRef")]
    internal static IDisposable? GetValueOrDefault([NotNullIfNotNull("fieldRef")] /*in*/ ref IDisposable? fieldRef)
    {
        var current = Volatile.Read(ref fieldRef);

        return current == BooleanDisposable.True
            ? EmptyDisposable.Instance
            : current;
    }
    
    internal static TrySetSingleResult TrySetSingle([NotNullIfNotNull("value")] ref IDisposable? fieldRef,
        IDisposable? value)
    {
        var old = Interlocked.CompareExchange(ref fieldRef, value, null);
        if (old == null) return TrySetSingleResult.Success;

        if (old != BooleanDisposable.True) return TrySetSingleResult.AlreadyAssigned;

        value?.Dispose();
        return TrySetSingleResult.Disposed;
    }
    
    internal static bool TrySetMultiple([NotNullIfNotNull("value")] ref IDisposable? fieldRef, IDisposable? value)
    {
        var old = Volatile.Read(ref fieldRef);

        for (;;)
        {
            if (old == BooleanDisposable.True)
            {
                value?.Dispose();
                return false;
            }

            var b = Interlocked.CompareExchange(ref fieldRef, value, old);

            if (old == b) return true;

            old = b;
        }
    }
    internal static bool TrySetSerial([NotNullIfNotNull("value")] ref IDisposable? fieldRef, IDisposable? value)
    {
        var copy = Volatile.Read(ref fieldRef);
        for (;;)
        {
            if (copy == BooleanDisposable.True)
            {
                value?.Dispose();
                return false;
            }

            var current = Interlocked.CompareExchange(ref fieldRef, value, copy);
            if (current == copy)
            {
                copy?.Dispose();
                return true;
            }

            copy = current;
        }
    }
    internal static void Dispose([NotNullIfNotNull("fieldRef")] ref IDisposable? fieldRef)
    {
        var old = Interlocked.Exchange(ref fieldRef, BooleanDisposable.True);

        if (old != BooleanDisposable.True) old?.Dispose();
    }
}

internal sealed class AnonymousDisposable : ICancelable
{
    private volatile Action? _dispose;
    
    public AnonymousDisposable(Action dispose)
    {
        Debug.Assert(dispose != null);

        _dispose = dispose;
    }
    
    public bool IsDisposed => _dispose == null;
    
    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

internal sealed class AnonymousDisposable<TState> : ICancelable
{
    private          TState          _state;
    private volatile Action<TState>? _dispose;
    
    public AnonymousDisposable(TState state, Action<TState> dispose)
    {
        Debug.Assert(dispose != null);

        _state   = state;
        _dispose = dispose;
    }
    
    public bool IsDisposed => _dispose == null;
    
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke(_state);
        _state = default!;
    }
}

internal sealed class BooleanDisposable : ICancelable
{
    internal static readonly BooleanDisposable True = new(true);

    private volatile bool _isDisposed;
    public BooleanDisposable()
    {
    }

    private BooleanDisposable(bool isDisposed) => _isDisposed = isDisposed;
    
    public bool IsDisposed => _isDisposed;
    
    public void Dispose() => _isDisposed = true;
}

internal struct SingleAssignmentDisposableValue
{
    private IDisposable? _current;
    
    public bool IsDisposed => Volatile.Read(ref _current) == BooleanDisposable.True;
    
    public IDisposable? Disposable
    {
        get => Disposables.GetValueOrDefault(ref _current);
        set
        {
            var result = Disposables.TrySetSingle(ref _current, value);

            if (result == TrySetSingleResult.AlreadyAssigned)
                throw new InvalidOperationException(Strings_Core.DISPOSABLE_ALREADY_ASSIGNED);
        }
    }
    
    public void Dispose() => Disposables.Dispose(ref _current);
}

internal enum TrySetSingleResult
{
    Success,
    AlreadyAssigned,
    Disposed
}

internal interface ISafeObserver<T> : IRefObserver<T>, IDisposable
{
    void SetResource(IDisposable resource);
}

internal interface IRefObserver<T> : IObserver<T>
{
    void OnNext(ref T value);
}

internal interface IRefObservable<T> : IObservable<T>
{
    IDisposable Subscribe(IRefObserver<T> observer);
}

internal static class Stubs
{
    public static readonly Action            Nop   = static () => { };
    public static readonly Action<Exception> Throw = static ex => { ex.Throw(); };
}

internal static class ExceptionHelpers
{
    private static readonly Lazy<IExceptionServices> Services = new(Initialize);

    [DoesNotReturn]
    public static void Throw(this Exception exception) => Services.Value.Rethrow(exception);

    private static IExceptionServices Initialize()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return PlatformEnlightenmentProvider.Current.GetService<IExceptionServices>() ?? new DefaultExceptionServices();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    internal sealed class DefaultExceptionServices /*Impl*/ : IExceptionServices
    {
#pragma warning disable CS8763 // NB: On down-level platforms, Throw is not marked as DoesNotReturn.
        [DoesNotReturn]
        public void Rethrow(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();
#pragma warning restore CS8763
    }
}

[System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
[DebuggerNonUserCode]
[CompilerGeneratedAttribute]
internal class Strings_Core
{
    private static System.Resources.ResourceManager resourceMan;

    private static System.Globalization.CultureInfo resourceCulture;

    [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal Strings_Core()
    {
    }

    /// <summary>
    ///   Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (ReferenceEquals(resourceMan, null))
            {
                var temp = new System.Resources.ResourceManager("System.Reactive.Strings_Core",
                    typeof(Strings_Core).GetTypeInfo().Assembly);
                resourceMan = temp;
            }

            return resourceMan;
        }
    }

    /// <summary>
    ///   Overrides the current thread's CurrentUICulture property for all
    ///   resource lookups using this strongly typed resource class.
    /// </summary>
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static System.Globalization.CultureInfo Culture
    {
        get => resourceCulture;
        set => resourceCulture = value;
    }

    /// <summary>
    ///   Looks up a localized string similar to Using the Scheduler.{0} property is no longer supported due to refactoring of the API surface and elimination of platform-specific dependencies. Please include System.Reactive.PlatformServices for your target platform and use the {0}Scheduler type instead. If you&apos;re building a Windows Store app, notice some schedulers are no longer supported. Consider using Scheduler.Default instead..
    /// </summary>
    internal static string CANT_OBTAIN_SCHEDULER => ResourceManager.GetString("CANT_OBTAIN_SCHEDULER", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to OnCompleted notification doesn't have a value..
    /// </summary>
    internal static string COMPLETED_NO_VALUE => ResourceManager.GetString("COMPLETED_NO_VALUE", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Disposable has already been assigned..
    /// </summary>
    internal static string DISPOSABLE_ALREADY_ASSIGNED =>
        ResourceManager.GetString("DISPOSABLE_ALREADY_ASSIGNED", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Disposables collection can not contain null values..
    /// </summary>
    internal static string DISPOSABLES_CANT_CONTAIN_NULL =>
        ResourceManager.GetString("DISPOSABLES_CANT_CONTAIN_NULL", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Failed to start monitoring system clock changes..
    /// </summary>
    internal static string FAILED_CLOCK_MONITORING =>
        ResourceManager.GetString("FAILED_CLOCK_MONITORING", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Heap is empty..
    /// </summary>
    internal static string HEAP_EMPTY => ResourceManager.GetString("HEAP_EMPTY", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Observer has already terminated..
    /// </summary>
    internal static string OBSERVER_TERMINATED => ResourceManager.GetString("OBSERVER_TERMINATED", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to Reentrancy has been detected..
    /// </summary>
    internal static string REENTRANCY_DETECTED => ResourceManager.GetString("REENTRANCY_DETECTED", resourceCulture);

    /// <summary>
    ///   Looks up a localized string similar to This scheduler operation has already been awaited..
    /// </summary>
    internal static string SCHEDULER_OPERATION_ALREADY_AWAITED =>
        ResourceManager.GetString("SCHEDULER_OPERATION_ALREADY_AWAITED", resourceCulture);
}