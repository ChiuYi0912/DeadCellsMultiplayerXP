using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeadCellsMultiplayerX.Engine.Serialization;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.Loadout;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using MessagePack;
using MessagePack.Resolvers;
using Nerdbank.Streams;
using PolyType.SourceGenerator;
using StreamJsonRpc;

using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
namespace DeadCellsMultiplayerX.Tools
{
    /// <summary>
    /// Phase 0 协议基线生成器（开发期工具，不参与模组运行时）。
    /// 使用固定 DTO、项目现有 MessagePack 配置和真实 RPC 契约/格式器，
    /// 生成后续重构可逐字节/逐方法对比的基线文件。
    /// </summary>
    public static class ProtocolBaseline
    {
        private const int SCHEMA_VERSION = 1;

        private static readonly MessagePackSerializerOptions PureOptions =
            MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance);

        /// <summary>
        /// 游戏内 DEBUG 基线入口：由环境变量触发，生成完整 serializer 基线后由调用方退出。
        /// </summary>
        public static bool TryRunFromEnvironment()
        {
            string? outputPath = Environment.GetEnvironmentVariable(
                "DCMX_PROTOCOL_BASELINE_OUTPUT");

            if (string.IsNullOrWhiteSpace(outputPath))
                return false;

            Run(outputPath);
            return true;
        }

        public static void Run(string outputPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            var document = new Dictionary<string, object?>
            {
                ["schema"] = SCHEMA_VERSION,
                ["generator"] = "DeadCellsMultiplayerX.Tools.ProtocolBaseline",
            };

            var fixtures = CreateFixtures();
            bool fullSerializerAvailable = true;
            string? fullSerializerError = null;

            try
            {
                // 触发 DCMXSerializers.MessagePack 的静态初始化。
                _ = DCMXSerializers.MessagePack.Serialize(
                    fixtures[0].Value,
                    fixtures[0].WireType);
            }
            catch (Exception ex)
            {
                fullSerializerAvailable = false;
                fullSerializerError = Describe(ex);
            }

            document["fullSerializer"] = new Dictionary<string, object?>
            {
                ["available"] = fullSerializerAvailable,
                ["error"] = fullSerializerError,
            };

            var dtoEntries = new List<Dictionary<string, object?>>();
            foreach (var fixture in fixtures)
            {
                var entry = new Dictionary<string, object?>
                {
                    ["type"] = fixture.Name,
                    ["wireType"] = fixture.WireType.FullName,
                };

                if (fullSerializerAvailable)
                {
                    try
                    {
                        entry["full"] = SerializeFull(fixture.Value, fixture.WireType);
                    }
                    catch (Exception ex)
                    {
                        entry["fullError"] = Describe(ex);
                    }
                }

                try
                {
                    entry["contractless"] = SerializePure(fixture.Value, fixture.WireType);
                }
                catch (Exception ex)
                {
                    entry["contractlessError"] = Describe(ex);
                }

                dtoEntries.Add(entry);
            }

            dtoEntries.Sort((a, b) =>
                string.CompareOrdinal((string?)a["type"], (string?)b["type"]));
            document["dtos"] = dtoEntries;

            document["rpcMethods"] = CollectRpcMethods();

            try
            {
                document["rpcLoopback"] = RunRpcLoopback();
            }
            catch (Exception ex)
            {
                document["rpcLoopback"] = new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["error"] = Describe(ex),
                };
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                document,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        }

        private static Dictionary<string, object?> SerializeFull(
            object value,
            Type wireType)
        {
            byte[] bytes = DCMXSerializers.MessagePack.Serialize(value, wireType);
            return Payload(bytes);
        }

        private static Dictionary<string, object?> SerializePure(
            object value,
            Type wireType)
        {
            byte[] bytes = MessagePackSerializer.Serialize(wireType, value, PureOptions);
            return Payload(bytes);
        }

        private static Dictionary<string, object?> Payload(byte[] bytes)
        {
            return new Dictionary<string, object?>
            {
                ["length"] = bytes.Length,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                ["base64"] = Convert.ToBase64String(bytes),
            };
        }

        private static Dictionary<string, string[]> CollectRpcMethods()
        {
            return new Dictionary<string, string[]>
            {
                [nameof(IServerRPC)] = MethodNames(typeof(IServerRPC)),
                [nameof(IGuestRPC)] = MethodNames(typeof(IGuestRPC)),
                [nameof(IHostClientRPC)] = MethodNames(typeof(IHostClientRPC)),
            };
        }

        private static string[] MethodNames(Type contract)
        {
            return contract
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static Dictionary<string, object?> RunRpcLoopback()
        {
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            var serverFormatter = new NerdbankMessagePackFormatter
            {
                TypeShapeProvider = TypeShapeProvider_DeadCellsMultiplayerX.Default,
            };
            var clientFormatter = new NerdbankMessagePackFormatter
            {
                TypeShapeProvider = TypeShapeProvider_DeadCellsMultiplayerX.Default,
            };

            using var serverRpc = new JsonRpc(
                new LengthHeaderMessageHandler(serverStream, serverStream, serverFormatter));
            using var clientRpc = new JsonRpc(
                new LengthHeaderMessageHandler(clientStream, clientStream, clientFormatter));

            serverRpc.AddLocalRpcTarget(new BaselineRpcTarget());
            serverRpc.StartListening();
            clientRpc.StartListening();

            long result = clientRpc.InvokeAsync<long>(nameof(BaselineRpcTarget.GetTimeStamp))
                .GetAwaiter()
                .GetResult();

            return new Dictionary<string, object?>
            {
                ["ok"] = result == BaselineRpcTarget.TimeStamp,
                ["method"] = nameof(BaselineRpcTarget.GetTimeStamp),
                ["result"] = result,
                ["transport"] = "LengthHeaderMessageHandler+NerdbankMessagePackFormatter",
            };
        }

        private static string Describe(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException != null)
                current = current.InnerException;

            return $"{current.GetType().FullName}: {current.Message}";
        }

        private static List<DtoFixture> CreateFixtures()
        {
            var heroSpawn = CreateHeroSpawn();
            var heroDynamic = CreateHeroDynamic();
            var entitySpawn = CreateEntitySpawn();
            var entityDynamic = CreateEntityDynamic();
            var entityDespawn = CreateEntityDespawn();
            var areaState = CreateAreaState();
            var action = CreateHeroAction();
            var actionEvent = CreateHeroActionEvent();
            var mobEvent = CreateMobModifierEvent();
            var worldFrame = CreateWorldFrame(
                heroDynamic, heroSpawn, entityDynamic, entitySpawn,
                entityDespawn, areaState, actionEvent, mobEvent);
            var clientWorldFrame = CreateClientWorldFrame(heroSpawn, heroDynamic);
            var worldResync = CreateWorldResync(
                heroDynamic, heroSpawn, entityDynamic, entitySpawn, areaState);
            var entityInfo = CreateEntityInfo();
            var lobbyInfo = CreateLobbyInfo();

            return
            [
                new("HelloRequest", new HelloRequest
                {
                    MAJOR = ProtocolVersion.MAJOR,
                    MINOR = ProtocolVersion.MINOR,
                    ModVersion = "baseline",
                    Capabilities = ProtocolVersion.SUPPORTED_CAPABILITIES,
                }),
                new("HelloResponse", new HelloResponse
                {
                    Accepted = true,
                    ServerMajor = ProtocolVersion.MAJOR,
                    ServerMinor = ProtocolVersion.MINOR,
                    Reason = string.Empty,
                    Epoch = 7,
                    Capabilities = ProtocolVersion.SUPPORTED_CAPABILITIES,
                }),
                new("ItemRef", CreateItemRef("BaselineSword", 0, 1)),
                new("HeroAction", action),
                new("HeroActionEvent", actionEvent),
                new("PlayerLoadout", CreatePlayerLoadout()),
                new("EquipSlotKey", new EquipmentSlotKey(EquipmentCategory.Weapon, 0)),
                new("ItemDef", CreateItemDef("BaselineSword", 1, 5)),
                new("ItemRuntime", new ItemRuntime { Ammo = 7, CooldownUntilMs = 123.5 }),
                new("HeroSpawn", heroSpawn),
                new("HeroDynamic", heroDynamic),
                new("EntitySpawn", entitySpawn),
                new("EntityDynamic", entityDynamic),
                new("EntityDespawn", entityDespawn),
                new("LevelAreaAffectState", areaState),
                new("WorldFrame", worldFrame),
                new("ClientWorldFrame", clientWorldFrame),
                new("WorldResync", worldResync),
                new("WorldFrameRangeRequest", new WorldFrameRangeRequest
                {
                    FromSequence = 10,
                    ToSequence = 20,
                }),
                new("ClientFrameRangeRequest", new ClientFrameRangeRequest
                {
                    FromSequence = 30,
                    ToSequence = 40,
                }),
                new("WorldFrameBundle", new WorldFrameBundle
                {
                    Frames = [worldFrame],
                    Resync = worldResync,
                }),
                new("ClientFrameBundle", new ClientFrameBundle
                {
                    Frames = [clientWorldFrame],
                    Unavailable = false,
                }),
                new("MobModifierEvent", mobEvent),
                new("EntityEventBase", mobEvent, typeof(EntityEvent)),
                new("LobbyInfo", lobbyInfo),
                new("GuestInfo", CreateGuestInfo()),
                new("LevelVote", new LevelVote
                {
                    PlyerID = "00000000-0000-0000-0000-000000000001",
                    NextLevelID = "PrisonStart",
                    LevelSelected = true,
                }),
                new("AreaInfo", new AreaInfo
                {
                    Rect = new RectInt(1, 2, 3, 4),
                    Collision = [1, 2, 3, 4],
                    Entities = [entityInfo],
                }),
                new("EntityInfo", entityInfo),
                new("PosVector", CreatePosVector()),
                new("AnimInfo", CreateAnimInfo()),
                new("SpriteInfo", CreateSpriteInfo()),
                new("AffectEntry", CreateAffectEntry()),
            ];
        }

        private static HeroAction CreateHeroAction()
        {
            return new HeroAction
            {
                Epoch = 7,
                ActionId = 42,
                Kind = HeroActionKind.Weapon,
                Slot = 0,
                Item = CreateItemRef("BaselineSword", 0, 1),
                CX = 10,
                CY = 20,
                XR = 0.5,
                YR = 0.25,
                Dir = 1,
                DX = 1.5,
                DY = -0.5,
                Ratio = 1.0,
                Cycle = 2,
                TargetNetId = 1001,
                ClientTimeMs = 123456,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                BrutalityTier = 3,
                SurvivalTier = 4,
                TacticTier = 5,
                ShieldKind = ShieldActionKind.Counter,
                FullParry = true,
                SourceNetId = 1001,
                SourceActionId = 77,
            };
        }

        private static HeroActionEvent CreateHeroActionEvent()
        {
            return new HeroActionEvent
            {
                Epoch = 7,
                ActionId = 42,
                CasterGuid = "00000000-0000-0000-0000-000000000001",
                Kind = HeroActionKind.Weapon,
                Slot = 0,
                Item = CreateItemRef("BaselineSword", 0, 1),
                CX = 10,
                CY = 20,
                XR = 0.5,
                YR = 0.25,
                Dir = 1,
                DX = 1.5,
                DY = -0.5,
                Ratio = 1.0,
                Cycle = 2,
                BrutalityTier = 3,
                SurvivalTier = 4,
                TacticTier = 5,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                ServerTimeMs = 123999,
                ShieldKind = ShieldActionKind.Counter,
                FullParry = true,
                SourceNetId = 1001,
                SourceActionId = 77,
            };
        }

        private static PlayerLoadout CreatePlayerLoadout()
        {
            return new PlayerLoadout
            {
                Version = 1234,
                Weapons =
                [
                    CreateEquipmentSlot("BaselineSword", 1, 0, 5),
                    CreateEquipmentSlot("BaselineShield", 1, 1, 3),
                ],
                Actives =
                [
                    CreateEquipmentSlot("BaselineTrap", 2, 0, 2),
                    CreateEquipmentSlot("BaselineGrenade", 2, 1, 2),
                ],
                Talisman = CreateEquipmentSlot("BaselineAmulet", 3, 0, 4),
                BrutalityTier = 3,
                SurvivalTier = 4,
                TacticTier = 5,
            };
        }

        private static EquipmentSlot CreateEquipmentSlot(
            string id, int kind, int posId, int level)
        {
            return new EquipmentSlot
            {
                Def = CreateItemDef(id, kind, level, posId),
                Runtime = new ItemRuntime
                {
                    Ammo = 7,
                    CooldownUntilMs = 123.5,
                },
            };
        }

        private static ItemDef CreateItemDef(
            string id, int kind, int level, int posId = 0)
        {
            return new ItemDef
            {
                Kind = kind,
                ItemId = id,
                Level = level,
                PermanentId = 100 + posId,
                Affixes = ["AffixA", "AffixB"],
                InactiveAffixes = ["InactiveA"],
            };
        }

        private static ItemRef CreateItemRef(string id, int posId, int kind)
        {
            return new ItemRef
            {
                Kind = kind,
                ItemId = id,
                ItemLevel = 5,
                PosID = posId,
                PermanentId = 42,
                Ammo = 7,
                Affixes = ["AffixA", "AffixB"],
                InactiveAffixes = ["InactiveA"],
            };
        }

        private static HeroSpawn CreateHeroSpawn()
        {
            return new HeroSpawn
            {
                GUID = "00000000-0000-0000-0000-000000000001",
                TypeName = "BaselineHero",
                ColorMapModel = "BaselineModel",
                ColorMapSkin = "BaselineSkin",
                SubLevelId = 0,
                MainSprite = CreateSpriteInfo(),
                GlowData = new Dictionary<int, byte[]>
                {
                    [0] = [1, 2, 3],
                    [1] = [4, 5],
                },
                LevelId = "PrisonStart",
                Version = 3,
                Talisman = CreateItemRef("BaselineAmulet", 2, 3),
                Loadout = CreatePlayerLoadout(),
            };
        }

        private static HeroDynamic CreateHeroDynamic()
        {
            return new HeroDynamic
            {
                GUID = "00000000-0000-0000-0000-000000000001",
                ChangeMask = HeroDynamic.BIT_ALL,
                Pos = CreatePosVector(),
                Anim = CreateAnimInfo(),
                Affects = [CreateAffectEntry()],
                Flags = 0,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                SpawnVersion = 3,
                Life = 100,
                MaxLife = 120,
                IsDead = false,
            };
        }

        private static EntitySpawn CreateEntitySpawn()
        {
            return new EntitySpawn
            {
                NetId = 1001,
                Kind = EntityKind.Mob,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                EntityData = [1, 2, 3, 4],
            };
        }

        private static EntityDynamic CreateEntityDynamic()
        {
            return new EntityDynamic
            {
                NetId = 1001,
                Kind = EntityKind.Mob,
                Pos = CreatePosVector(),
                Anim = CreateAnimInfo(),
                Life = 10,
                MaxLife = 20,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                Affects = [CreateAffectEntry()],
                Extra = [9, 8, 7],
            };
        }

        private static EntityDespawn CreateEntityDespawn()
        {
            return new EntityDespawn
            {
                NetId = 1001,
                Kind = EntityKind.Mob,
                Reason = EntityDespawnReason.Killed,
            };
        }

        private static LevelAreaAffectState CreateAreaState()
        {
            return new LevelAreaAffectState
            {
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                Cx = 10,
                Cy = 20,
                A = 3,
                ADurationS = 2.5,
                AValue = 1.25,
                RemainingSec = 1.75,
                ServerTimeSec = 123.5,
                Affixes = ["AffixA"],
            };
        }

        private static MobModifierEvent CreateMobModifierEvent()
        {
            return new MobModifierEvent
            {
                Epoch = 7,
                NetId = 1001,
                EntityKind = EntityKind.Mob,
                ServerTimeMs = 123999,
                LevelId = "PrisonStart",
                SubLevelIndex = 0,
                ActionId = 55,
                Kind = MobModifierEventKind.AttackHit,
                SkillId = "BaselineSkill",
                TargetGuid = "00000000-0000-0000-0000-000000000001",
                Data = "baseline",
                SkillRatio = 1.0,
            };
        }

        private static WorldFrame CreateWorldFrame(
            HeroDynamic heroDynamic,
            HeroSpawn heroSpawn,
            EntityDynamic entityDynamic,
            EntitySpawn entitySpawn,
            EntityDespawn entityDespawn,
            LevelAreaAffectState areaState,
            HeroActionEvent actionEvent,
            MobModifierEvent mobEvent)
        {
            return new WorldFrame
            {
                Epoch = 7,
                Sequence = 99,
                ServerTimeMs = 123456.789,
                Heroes = [heroDynamic],
                Spawned = [heroSpawn],
                Despawned = ["00000000-0000-0000-0000-000000000002"],
                Entities = [entityDynamic],
                SpawnedEntities = [entitySpawn],
                DespawnedEntities = [entityDespawn],
                Actions = [actionEvent],
                AreaAffects = [areaState],
                EntityEvents = [mobEvent],
            };
        }

        private static ClientWorldFrame CreateClientWorldFrame(
            HeroSpawn heroSpawn,
            HeroDynamic heroDynamic)
        {
            return new ClientWorldFrame
            {
                Epoch = 7,
                Sequence = 42,
                ClientTimeMs = 123456.789,
                FrameId = 17,
                Heroes =
                [
                    new HeroUpload
                    {
                        ClientTime = 123456,
                        FrameId = 17,
                        Spawn = heroSpawn,
                        Dynamic = heroDynamic,
                    },
                ],
            };
        }

        private static WorldResync CreateWorldResync(
            HeroDynamic heroDynamic,
            HeroSpawn heroSpawn,
            EntityDynamic entityDynamic,
            EntitySpawn entitySpawn,
            LevelAreaAffectState areaState)
        {
            return new WorldResync
            {
                Epoch = 7,
                Sequence = 99,
                ServerTimeMs = 123456.789,
                Heroes = [heroDynamic],
                Spawns = [heroSpawn],
                Entities = [entityDynamic],
                EntitySpawns = [entitySpawn],
                AreaAffects = [areaState],
            };
        }

        private static EntityInfo CreateEntityInfo()
        {
            return new EntityInfo
            {
                TypeName = "BaselineMob",
                GUID = "00000000-0000-0000-0000-000000000003",
                remoteTime = 1.5,
                localTime = 2.5,
                ColorMapModel = "BaselineModel",
                ColorMapSkin = "BaselineSkin",
                SubLevelId = 0,
                PosVector = CreatePosVector(),
                GlowData = new Dictionary<int, byte[]>
                {
                    [0] = [1, 2, 3],
                },
                MainSprite = CreateSpriteInfo(),
                animInfo = CreateAnimInfo(),
                CollisionMode = 1,
                HeroEffectList = [CreateAffectEntry()],
            };
        }

        private static PosVector CreatePosVector()
        {
            var pos = new PosVector(10, 20, 0.5, 0.25, 1)
            {
                DX = 1.5,
                DY = -0.5,
                BDX = 0.25,
                BDY = -0.25,
            };
            return pos;
        }

        private static AnimInfo CreateAnimInfo()
        {
            var anim = new AnimInfo
            {
                GroupName = "run",
                StartTime = 1234,
                Speed = 1.5,
                Frame = 12,
                Paused = false,
                Plays = 2,
                playDuration = 0.5,
                animCursor = 3,
            };

            anim.AnimTransitions.Add(new AnimTransitions
            {
                Anim = "run",
                From = "idle",
                To = "run",
                reverse = false,
                speed = 1.0,
            });

            return anim;
        }

        private static SpriteInfo CreateSpriteInfo()
        {
            return new SpriteInfo
            {
                GUID = "00000000-0000-0000-0000-000000000004",
                Parent = "BaselineParent",
                AtlasName = "atlas/baseline.atlas",
                GroupName = "baseline",
                PivotData = [1, 2, 3],
                Children = [],
            };
        }

        private static AffectEntry CreateAffectEntry()
        {
            return new AffectEntry
            {
                OuterIndex = 0,
                InnerIndex = 1,
                A = 3,
                T = 2.5,
                UniqId = 9,
                Val = 1.25,
                StartSec = 10.5,
                EndSec = 13.0,
                RemainingSec = 2.5,
                ServerTimeSec = 123.5,
            };
        }

        private static LobbyInfo CreateLobbyInfo()
        {
            return new LobbyInfo
            {
                Guests = new Dictionary<string, GuestInfo>
                {
                    ["00000000-0000-0000-0000-000000000001"] = CreateGuestInfo(),
                },
                IsStarted = true,
                CanConnectServer = true,
                Owner = "00000000-0000-0000-0000-000000000001",
                GameMode = GameMode.Normal,
            };
        }

        private static GuestInfo CreateGuestInfo()
        {
            return new GuestInfo
            {
                Name = "BaselineGuest",
                Guid = "00000000-0000-0000-0000-000000000001",
                IsReady = true,
                IsHost = true,
                IsConnectedServer = true,
                SkinMould = "BaselineSkin",
            };
        }

        private sealed class DtoFixture
        {
            public DtoFixture(string name, object value, Type? wireType = null)
            {
                Name = name;
                Value = value;
                WireType = wireType ?? value.GetType();
            }

            public string Name { get; }
            public object Value { get; }
            public Type WireType { get; }
        }

        private sealed class BaselineRpcTarget
        {
            public const long TimeStamp = 123456789L;

            public Task<long> GetTimeStamp() => Task.FromResult(TimeStamp);
        }
    }
}
