namespace DeadCellsMultiplayerX.Engine.Serialization;

using System.Buffers;

public interface IBinarySerializer
{
    public SerializerKind Kind { get; }

    public byte[] Serialize<T>(T? obj);

    public T? Deserialize<T>(ReadOnlySpan<byte> data);



    public object? Deserialize(ReadOnlySpan<byte> data, Type type);

    public byte[] Serialize(object obj, Type type);



    public void Serialize<T>(IBufferWriter<byte> writer, T? obj);

    public void Serialize(
        IBufferWriter<byte> writer,
        object obj,
        Type type);

    public T? Deserialize<T>(byte[] data)
        => this.Deserialize<T>((ReadOnlySpan<byte>)data);

    public object? Deserialize(byte[] data, Type type)
        => this.Deserialize((ReadOnlySpan<byte>)data, type);
}

public static class DCMXSerializers
{
    public static IBinarySerializer MessagePack { get; }
        = MessagePackBinarySerializer.Instance;
}

public enum SerializerKind : byte
{
    MessagePack = 2
}
