using System;

namespace DeadCellsMultiplayerX.Common.Contracts.Protocol
{
    public static class ProtocolVersion
    {
        public const int Major = 1;

        public const int Minor = 0;

        public const uint RequiredCapabilities = (uint)ProtocolCapabilities.TypedActions;

        public const uint SupportedCapabilities = (uint)(ProtocolCapabilities.TypedActions | ProtocolCapabilities.ServerShieldState);

        public static string Describe()
            => $"{Major}.{Minor}";
    }

    [Flags]
    public enum ProtocolCapabilities : uint
    {
        None = 0,

        TypedActions = 1u << 0,

        ServerShieldState = 1u << 1,
    }
}
