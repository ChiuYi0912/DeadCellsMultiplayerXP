namespace DeadCellsMultiplayerX.Engine.Serialization;

using System.Buffers;
using dc.level;
using DeadCellsMultiplayerX.Engine.Serialization.Data;
using MessagePack;
using MessagePack.Resolvers;

public static class GameDataSerializer
{

    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard
            .WithResolver(
                CompositeResolver.Create(
                    [
                        new H3DVectorFormatter(),
                        new H3DTextureFormatter(),
                        new HxbitMessagePackFormatter<Mob>(Mob.Class,Mob.Class.__clid)
                    ],
                    [
                        ContractlessStandardResolver.Instance,
                        HaxeFallbackResolver.Instance,
                    ]
                )
            );

    public static byte[] Serialize<T>(T? obj) => MessagePackSerializer.Serialize(obj, Options);

    public static byte[] Serialize(object obj, Type type) => MessagePackSerializer.Serialize(type, obj, Options);

    public static T? Deserialize<T>(byte[] data) => MessagePackSerializer.Deserialize<T>(data, Options);

    public static object? Deserialize(byte[] data, Type type) => MessagePackSerializer.Deserialize(type, data, Options);

    public static T? Deserialize<T>(ReadOnlySpan<byte> data) => MessagePackSerializer.Deserialize<T>(data.ToArray(), Options);

    public static object? Deserialize(ReadOnlySpan<byte> data, Type type) => MessagePackSerializer.Deserialize(type, data.ToArray(), Options);

    public static void Serialize<T>(IBufferWriter<byte> writer, T? obj) => MessagePackSerializer.Serialize(writer, obj, Options);

    public static void Serialize(IBufferWriter<byte> writer, object obj, Type type) => MessagePackSerializer.Serialize(type, writer, obj, Options);
}
