using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace RxTest;

public struct TestEvent : IEvent
{
    public bool Handled { get; set; }
}

public interface IEvent
{
    public bool Handled { get; }
}

public class EventBus
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new ();

    public void Publish<T>(T @event) where T : struct, IEvent => GetOrAddSubject<T>().OnNext(ref @event);

    public IDisposable Subscribe<T>(RefAction<T> action) where T : struct, IEvent => GetOrAddSubject<T>().Where(e => !e.Handled).Subscribe(action);

    private RefSubject<T> GetOrAddSubject<T>() where T : struct => Unsafe.As<RefSubject<T>>(_subjects.GetOrAdd(typeof(T), _ => new RefSubject<T>()));
}