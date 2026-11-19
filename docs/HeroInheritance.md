# Hero 继承基础与渐进接入边界

## 本次落地

```text
dc.en.hero.Beheaded
└── Engine.Overrides.MultiplayerHero (abstract)
    ├── Client.Overrides.ClientHeroEntity
    └── Server.Overrides.ServerHeroEntity
```

三个类型均为 internal，两个具体类型为 sealed，仍可在各自实现中 override 继承的原生虚方法；目前没有继续增加派生层级的需求。构造函数只沿用 `Beheaded(Game)`，共同基类在原生分配前拒绝 null Game。不增加字段、行为重写、生命周期回调、接口、注册器或工厂。

现有创建入口、Hook、实体管理、协议、序列化和更新顺序保持原样。本次不创建上述新类型的实例，因此没有新的显示对象、原生管理器、碰撞体或更新任务。“隐藏试运行”属于后续接入步骤，本次不通过构造函数修改 visible、队伍或碰撞设置来模拟隔离。

选择 Entity 后缀是为了区分原生实体与现有 `Server.World.Heroes.ServerHero`：后者是拥有 GUID、装备镜像、命令账本和战斗代理的玩家聚合，不是 Beheaded 子类。新的继承体系独立放在各端的 Overrides 命名空间，与现有联机 Hero 实现分开；保留现有类型名称和行为。

## 目录与命名空间归属

```text
Engine/
├── Overrides/MultiplayerHero.cs
└── Adapters/Heroes/                  # 现有原生代理工厂及技能、武器适配
Client/
├── Overrides/ClientHeroEntity.cs
└── World/Heroes/                     # 现有 Local、Remote、Replay
Server/
├── Overrides/ServerHeroEntity.cs
└── World/
    ├── ServerWorld.cs
    ├── ServerActorRuntime.cs         # 编排 Hero、仇恨和 Mob 运行时
    ├── Heroes/
    │   ├── ServerHero.cs             # 单个玩家的服务端聚合
    │   ├── ServerActorState.cs       # 玩家集合、Epoch 和动作结果存储
    │   ├── ServerHeroCombatProxy.cs
    │   ├── ServerHeroSpawner.cs
    │   ├── ServerTeamManager.cs
    │   ├── Replication/
    │   │   ├── HeroLoadoutMirror.cs
    │   │   ├── ServerHeroStateMirror.cs
    │   │   └── SpawnSynchronizer.cs
    │   ├── Actions/
    │   │   ├── HeroOutcomeJournal.cs
    │   │   ├── Commands/            # 原有命令处理
    │   │   ├── Execution/           # 原有动作执行
    │   │   └── Modules/             # 原有动作规则与模块目录
    │   └── Shield/ShieldAttackArbiter.cs
    ├── Mobs/Combat/ServerAggroManager.cs
    └── Replication/                  # 原有世界帧、实体和区域复制
```

原 Actors 目录移除：只管理玩家的类型归到 Heroes；客户端上传状态的装备与姿态镜像集中到 Heroes/Replication；动作结果队列与动作处理放在一起。ServerActorRuntime 同时编排 Hero 与 Mob，放在 World 层；ServerAggroManager 校验和清理怪物目标，归到 Mobs/Combat。这次只改变文件位置、命名空间及 using，不改变组合关系、调用顺序、方法实现或资源所有权。

Overrides 表示原生类型的派生与重写入口，不表示全局对象。未来实例仍由所属世界内的玩家所有者管理。现有 Engine/Adapters/Heroes 继续服务当前代理；它与 Overrides 的区别是“操作现有原生对象”和“定义新的原生派生类型”。

## 模块职责与依赖

| 类型或模块 | 所有权与职责 | 应留在其他位置的职责 |
| --- | --- | --- |
| MultiplayerHero | 游戏引擎适配层；未来统一修复两端都需要的 Beheaded 原生行为 | GUID、RPC、时钟、帧缓存、预测、动作回放、权威判定与客户端表现 |
| ClientHeroEntity | 客户端世界中的原生 Hero；未来承接客户端特有的移动、技能、武器等原生调用 | 网络接收和反序列化、回放去重、会话生命周期 |
| ServerHeroEntity | 服务端世界中的原生 Hero；未来承接服务端特有的原生战斗执行 | 连接、命令认证与去重、广播、跨关卡协调 |
| Client.World.Heroes.Remote | 当前可视化实体 RemoteHero 及其运动、动画、装备、动作表现组件 | 不因新类型存在而自动迁移 |
| Server.World.Heroes | 当前玩家聚合与战斗代理的所有者，集中 Hero 状态、动作和镜像同步 | 不成为 Beheaded 的基类；不编排整个 Mob 运行时 |
| Simulation | 当前命令账本、盾牌相位规则、装备数据转换；未来适合容纳不依赖原生对象的确定性规则 | 不引入 Beheaded、Game、客户端或 RPC 依赖 |

依赖方向为 `Client/Server → Engine → 游戏代理与运行时`。三个新类型均不引用 Simulation、Contracts 或 Networking。现有 `HeroActionRuntime`、GhostWeapons、GhostSkills 继续保留；不是为了建立继承关系而移动它们。

公共基类只接收两端确实相同的修复，不增加 `IsServer`、`IsLocal`、`IsPredicted` 等分支。未来预测与权威若共享纯规则，规则放到 Simulation，由各端调用并将结果应用到实体；共享规则不要求塞入实体基类。

## 已核实的原生构造与绑定

依据当前安装的 MDK 1.0.1 引用程序集、游戏缓存 GameProxy.dll、HaxeProxy.dll 的元数据和 IL，以及当前 hlboot.dat 的函数反汇编核实。仓库现有 `RemoteHero : KingSkin` 也展示了实际使用的原生派生方式。外部 SDK 源码副本与已安装二进制存在实现差异，本说明以已安装程序集为准。

1. `dc.en.hero.Beheaded` 未封闭，有 `Beheaded(dc.pr.Game)` 构造函数。生成 IL 依次调用 `HaxeProxyHelper.CreateInstance`、`HaxeProxyBase(HashlinkObj)` 和原生 `__inst_construct__`。因此不需要为三个类型添加新的 HashLink 类型索引或自制绑定属性。
2. `HaxeProxyBase` 在构造中将代理关联到原生对象，再调用 `HaxeProxyManager.CheckCustomProxy`。后者按实际 CLR 类型创建自定义 HashLink 类型并替换对象类型指针。现有对象已经持有代理身份，不能假定能把普通 Beheaded 强制转换或再次包装为新的派生类型。
3. `CustomHaxeType` 沿 CLR 继承链查找自定义类型声明的 public instance virtual 方法，并为对应的原生 proto 安装覆盖。`ProtoOverride` 要求原生 proto 也是虚方法。多层 CLR 继承可被扫描，但不能据此认定 HashLink 端存在完全相同的多层原生类型布局。
4. 本次核实可 override 的入口包括 `init`、`initGfx`、`wakeup`、`moveControlled`、`fixedUpdate`、`postUpdate`、`onDamage`、`canBeHitBy`、`destroy`、`dispose`。其中不少继承自 Hero 或 Entity。`clean`、`lockControlsS`、`onEquipedItemsChange` 是非虚方法；使用 `new` 只能隐藏 C# 成员，不能截获游戏中的原生调用。方法重载也要按实际签名和原生 proto 单独核对。
5. 当前 Beheaded 原生构造函数（f@18787，`Beheaded.hx:20–22`）直接创建 BeheadedWeaponsManager、BeheadedMainSkillsManager 和 BeheadedActiveSkillsManager。仅继承 Hero 不会自动替换这些管理器。Hero 原生构造函数（f@4901，`Hero.hx:224`）已经虚调用 createHead，绑定阶段也会调用 AfterBinding；未来 override 必须允许最派生构造函数尚未执行完成，不能假定构造体中赋值的组件已就绪。
6. `HaxeProxyManager.CreateProxy` 另有 `RuntimeHelpers.GetUninitializedObject` 路径，可跳过派生构造函数，再执行代理基类绑定。未来若添加必要的托管状态，必须同时核实正常创建和代理重建时的初始化策略。本次无新增状态或 AfterBinding 重写。
7. 派生实例仍继承原生 `getCLID`、序列化和静态创建逻辑。C# 类型存在不表示原生存档加载或 `Beheaded.Class.create` 会生成派生实例；自定义类型往返存档需要单独验证。本次不注册类型、不改 CLID，也不改网络 DTO。
8. 构造依赖已初始化的 HashLink 模块和有效的原生 Game，不能在普通无游戏运行时的测试进程中直接 new。未来接入应在游戏线程和正确的关卡生命周期内进行，RPC 或后台任务只交付待处理数据，不直接创建或操作原生 Hero。

原生虚分派可进入 override；静态调用、直接调用具体原生函数和非虚方法不一定经过它。现有 Hook 同样仍然有效：新 override 是否调用 base，会影响相应原生函数及 Hook 是否执行。不能直接删除旧 Hook，也不能同时从 native Tick 和外部循环驱动同一套技能/武器逻辑。

## 隐藏实体为何仍有副作用

`HeroProxyFactory.CreateClient` 设置 hero.visible、spr.visible 为 false，但仍创建攻击源、攻击目标并初始化头部；`RemoteHeroActionProxy` 继续通过 ClientHeroPresentationRuntime 调用技能和武器。现有隐藏实体本身就是“隐藏仍可执行”的具体例子。

原生 `Hero.fixedUpdate`（f@2155，`Hero.hx:5220–5222`）驱动 mainSkillsManager、weaponsManager、activeSkillsManager。`visible=false` 不是停止这些管理器的开关。现有 `HeroLoadoutRuntime.RestoreHudIcon` 也专门修复技能构造影响本地 HUD 的问题。不能把隐藏、禁用目标选择、禁用输入和禁用攻击视作同一个条件。

后续试运行前，应逐项建立并验证以下边界，而不是现在添加一个未经验证的“安全隐藏模式”：

| 风险 | 后续接入要求 |
| --- | --- |
| 重复更新、输入与移动 | 核对原生 pre/fixed/post 更新链、控制器及管理器调度；每项逻辑只能有一个驱动者。客户端远端实体不能读取本地输入，服务端实体只能消费已校验动作 |
| 重复碰撞、伤害与被选中 | 关闭新实体尚未接管的攻击源、攻击目标、碰撞、队伍可选中资格；逐项验证，不能只设置 targetable=false |
| 武器、技能与持续 Power | 每条动作只选择现有代理或新实体之一执行；隐藏实体不得产生第二份弹丸、召唤物、伤害区或持续效果 |
| 重复显示、声音与 UI | 除 sprite 外核对 heroHead、附属对象、武器特效、粒子、灯光、音频和 HUD。试运行期间 RemoteHero 继续独占正式表现 |
| Game.hero、相机、存档 | 不将试验实体写入 Game.hero，不接管相机/HUD，不加入正式存档；原生代码读取全局 Hero 的路径应逐项验证 |
| 退出、切关与延迟动作 | 先停止接收新动作、清除待执行动作，再销毁该 Hero 拥有的技能/武器及其派生对象，最后解除队伍/关卡引用并走经验证的原生销毁路径；迟到动作必须拒绝旧关卡或已销毁实体 |

上述要求是后续启用条件，不是本次已实现的能力。构造本身也会创建管理器并执行原生调用，不能先无条件创建一个隐藏实例，再期待之后补齐隔离。

## 生命周期与渐进替换

新实体未来属于当前关卡，不能由跨关卡稳定的连接或 ServerSession 直接持有。服务端沿用 `WorldCoordinator → ServerWorld → Actors → 玩家聚合 → 原生实体`；客户端沿用 `ClientWorld → 对应玩家所有者 → 原生实体`。客户端本地玩家和远端玩家的所有者不同，不能把 ClientHeroEntity 默认等同于远程可视化实体。本次不增加所有者代码。

1. **继承基础（本次）**：仅添加三个类型和文档，验证构建及无实例化引用；继续使用现有 RemoteHero 和服务端战斗代理。
2. **隔离验证**：在显式测试入口按关卡创建单个派生实体，验证 HashlinkObj 身份、原生虚方法回调、构造时序及销毁/重建。先完成上表的隔离，再进行隐藏对照。失败时不影响正式联机实体。
3. **逐个接入技能和武器**：按实际需要选择可 override 的行为或替换实体自身持有的原生管理器；旧管理器及其资源先按原生契约释放。非虚入口仅在确有需要时通过范围明确的 Hook 适配，不能用方法隐藏代替。客户端执行表现或预测，服务端执行校验后的权威结果；不让共同基类调用 RPC 或判断运行侧。
4. **明确更新与动作所有权**：每次迁移一个行为，让同一个动作只进入一条执行路径，维持现有 CommandId 去重和 Epoch 隔离。移动、动画、技能和战斗分别验证后再移交；同一状态不能被旧组件和新管理器同时写入。
5. **切换正式实现**：确认移动、动画、装备、碰撞、伤害、全局 Hero 依赖、存档与跨关卡清理后，再按玩家整体切换正式实体/表现所有权。旧代理停止创建与执行后才启用新路径。保留切回原实现的明确入口，避免运行中维护两套权威状态。

客户端预测、校正、历史状态和延迟补偿将来放在所属客户端/服务端流程或纯规则模块中；实体只应用所需结果、提供必要的原生行为入口。本次不预建这些系统。

## 验证范围

编译检查只能证明构造签名和继承结构与当前 SDK 兼容。本次不允许实例化或修改 Hook，因此不能宣称已验证新实体的游戏内创建、原生虚分派、隔离或销毁。正式接入前至少覆盖：无重复输入/更新/伤害、技能产生的 Power 清理、两次连续切关、断线重建、延迟动作拒绝、重新创建实体，以及存档加载仍保留原角色。
