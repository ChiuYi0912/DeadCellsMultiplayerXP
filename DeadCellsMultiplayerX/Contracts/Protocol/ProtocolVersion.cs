namespace DeadCellsMultiplayerX.Contracts.Protocol;

public static class ProtocolVersion
{
    public const int Major = 2;

    public const int Minor = 0;

    public const uint RequiredCapabilities = (uint)(
        ProtocolCapabilities.TypedActions
        | ProtocolCapabilities.ServerShieldState
        | ProtocolCapabilities.ReliableHeroCommands);

    public const uint SupportedCapabilities = (uint)(
        ProtocolCapabilities.TypedActions
        | ProtocolCapabilities.ServerShieldState
        | ProtocolCapabilities.ReliableHeroCommands);

    public static string Describe()
        => $"{Major}.{Minor}";
}

[Flags]
public enum ProtocolCapabilities : uint
{
    None = 0,

    TypedActions = 1u << 0,

    ServerShieldState = 1u << 1,

    ReliableHeroCommands = 1u << 2,
}
