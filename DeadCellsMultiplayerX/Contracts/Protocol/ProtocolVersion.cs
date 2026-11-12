namespace DeadCellsMultiplayerX.Contracts.Protocol
{
    public static class ProtocolVersion
    {
        public const int MAJOR = 1;

        public const int MINOR = 0;

        public const uint REQUIRED_CAPABILITIES = (uint)ProtocolCapabilities.TypedActions;

        public const uint SUPPORTED_CAPABILITIES = (uint)(ProtocolCapabilities.TypedActions | ProtocolCapabilities.ServerShieldState);

        public static string Describe()
            => $"{MAJOR}.{MINOR}";
    }

    [Flags]
    public enum ProtocolCapabilities : uint
    {
        None = 0,

        TypedActions = 1u << 0,

        ServerShieldState = 1u << 1,
    }
}
