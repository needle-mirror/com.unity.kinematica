namespace Unity.Kinematica.Editor
{
    public interface Payload<T>
    {
        T Build(PayloadBuilder builder);
    }
}
