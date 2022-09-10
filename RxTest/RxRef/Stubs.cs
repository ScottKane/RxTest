namespace RxTest;

internal static class Stubs
{
    public static readonly Action            Nop   = static () => { };
    public static readonly Action<Exception> Throw = static ex => { ex.Throw(); };
}