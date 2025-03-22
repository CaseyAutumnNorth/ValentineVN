namespace Naninovel
{
    /// <summary>
    /// Implantation is able to convert objects.
    /// </summary>
    public interface IConverter
    {
        object ConvertBlocking (object obj, string name);

        UniTask<object> Convert (object obj, string name);
    }

    /// <summary>
    /// Implantation is able to convert <typeparamref name="TSource"/> to <typeparamref name="TResult"/>.
    /// </summary>
    public interface IConverter<TSource, TResult> : IConverter
    {
        TResult ConvertBlocking (TSource obj, string name);

        UniTask<TResult> Convert (TSource obj, string name);
    }
}
