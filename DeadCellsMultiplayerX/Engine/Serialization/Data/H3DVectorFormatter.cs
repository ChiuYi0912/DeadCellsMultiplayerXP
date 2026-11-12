namespace DeadCellsMultiplayerX.Engine.Serialization.Data;

using dc.h3d;
using HaxeProxy.Runtime;
using MessagePack;
using MessagePack.Formatters;

public class H3DVectorFormatter : IMessagePackFormatter<Vector?>
{
    public void Serialize(ref MessagePackWriter writer, Vector? value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteArrayHeader(4);
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
        writer.Write(value.w);
    }

    public Vector? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return new Vector(Ref<double>.In(0), Ref<double>.In(0), Ref<double>.In(0), Ref<double>.In(0));
        }

        var count = reader.ReadArrayHeader();
        if (count != 4)
        {
            throw new InvalidOperationException($"Expected array of length 4, got {count}");
        }

        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        var w = reader.ReadDouble();
        return new Vector(Ref<double>.In(x), Ref<double>.In(y), Ref<double>.In(z), Ref<double>.In(w));
    }
}
