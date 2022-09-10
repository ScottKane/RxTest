using System.Diagnostics.CodeAnalysis;
using System.Reactive.PlatformServices;
using System.Runtime.ExceptionServices;

namespace RxTest;

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

    private sealed class DefaultExceptionServices /*Impl*/ : IExceptionServices
    {
#pragma warning disable CS8763 // NB: On down-level platforms, Throw is not marked as DoesNotReturn.
        [DoesNotReturn]
        public void Rethrow(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();
#pragma warning restore CS8763
    }
}