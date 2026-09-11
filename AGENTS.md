# AGENTS.md

## 项目现状（先读这条）

- **工程已初始化（2026-09-10）**：Unity 6 LTS 工程骨架已建（P2-5 落地，详见下文「Unity/C# 工程约定」）。Unity 编辑器本体经 Unity Hub 装于 Windows 侧 `D:\Unity\Hub\Editor`（版本 `6000.0.83f1`）；首次在 Windows 打开工程前注意 Hub 需登录并完成许可证激活。仓库已 `git init`，Git LFS 已本地启用。
- **设计事实来源（按优先级）**：
  1. [`Docs/关键数值与规则真源表_v1.4.md`](Docs/关键数值与规则真源表_v1.4.md)（《关键数值与规则真源表》）——跨文档复用的阈值/公式/CD/验收项的**唯一真源**；改数值先改此表再同步引用。
  2. [`Docs/情报与可信度系统详细规格_v1.2.md`](Docs/情报与可信度系统详细规格_v1.2.md)（《情报与可信度系统详细规格 v1.2》，含 2026-09-10 勘误与边界闭合批、2026-09-11 长平战役批）——情报生成、可信度公式、AI 信任桶（无方向载荷采信度/桶预养）、欺骗技能施计窗口+痕迹存续/识破、两类强制复核、UI 呈现、数据 schema 的规则真源。
  3. [`Docs/游戏设计文档_v1.3.md`](Docs/游戏设计文档_v1.3.md)（GDD v1.2，修订中）——总体设计；5 项 P0 与 9 项 P1 已全部关闭（双层时间、诡道技能槽位/反制、兵种克制/地形、战斗士气/补给/胜负、AI 优先级与玩家画像、幕僚系统、Unity 6 LTS、权威服务器架构），见附录 A；v1.2 为边界闭合批。
  4. [`Docs/游戏设计文档_v1.0.md`](Docs/游戏设计文档_v1.0.md) + [`Docs/游戏设计文档_v1.0_审校意见.md`](Docs/游戏设计文档_v1.0_审校意见.md)——历史基线与审校意见（P2 十二项中 P2-1/P2-7/P2-10 已关闭，其余待 P2 批合入）。
  5. 从属文档：[`Docs/基础兵种数值表_v0.3.md`](Docs/基础兵种数值表_v0.3.md)（含 COMBAT-16 地形减速 ×0.6 与赵骑地形 TTK 验算）、[`Docs/首发12计技能列表_v1.2.md`](Docs/首发12计技能列表_v1.2.md)（火攻已补效果；反间计 v1.2 定案为剧本事件，其余 4 计仍为机制草案）。
  6. 外部模型产出的平行稿/评审稿（曾称 deepseek.md 及 2026-09-10 的交叉评审报告）**不是事实来源**：平行稿含已被否决的旧值（Unity 2022 LTS、T/L/S 键位、按斥候实例计信源、§3 残章）；评审报告经逐条复核后仅部分成立（采纳项已在真源表 v1.1 变更记录留痕，驳回项亦记录理由），二者均仅可作素材参考。
- 文档冲突时：数值/阈值信真源表；情报/可信度/信任度规则信来源 2；其余信最新版 v1.2；审校意见是待决策建议而非已采纳设计。
- 文档与沟通语言为**中文**；代码、API、文件名保持英文/原样。

## P0 决策状态（2026-09-10 全部关闭）

- **P0-1 引擎**：Unity 6 LTS（C#），UE5 评估放弃（GDD §5.1 有决策记录）。
- **P0-2 时间**：双层模型（战略层=旬/回合制，战役层=秒/实时；1 叙事日=6 战场分钟，单局 15–20 分钟封顶，暂停冻结战役时钟）。
- **P0-3 克制环**：**枪兵/矛阵 → 轻骑 → 弩兵/弓兵 → 重步 → 枪兵**（克制 ×1.35、被克 ×0.75）；特色兵种 = 基础 4 类的七国强化/变体（GDD §2.4 映射表）。
- **P0-4 网络**：权威服务器 + 感知过滤，假实体服务端生成，真值永不下发（GDD §5.3）；原型单机但情报字段从原型起按"接收方/权威端"分离建模。
- **P0-5 情报可信度**：见独立规格文档。

## 范围控制（防止过度构建）

- 文档 §9.2 定义的 **MVP 只做**：赵、秦两个阵营；3 个诡道技能（减灶、增灶、火攻）；1 张地图（滏口陉–野王–高都）；幕僚 AI 仅廉颇 + 赵括；1 个战役剧本。
  - **2026-09-11 范围授权更新**：用户授权突破上述范围，首个战役改为**长平之战主战场**（第一张地图，MAP-09…16），滏口陉降为教学序章图；长平按史实做**七幕剧本**（CP-01…08，含换将/合围/断粮），并**首次实装廉颇+赵括幕僚**（ADV-07）。授权留痕见 GDD v1.3 附录 A.4。技能仍只用减灶/增灶/火攻三计，反间计为剧本事件不进技能系统（SKILL-13）。执行排期见 `Docs/长平之战首个战役完成计划_v0.1.md`。
  - **仍未授权**：七国全阵营、首发 12 计其余 9 计（4 计仍为草案）、多人对战、战役编辑器、苏代与其余阵营幕僚、L3 水墨地形 / L4 写实单位模型。
- 七国全阵营、三十六计全技能、多人对战、战役编辑器均属路线图（§6）第二阶段以后，未经明确要求不要提前实现。
- 核心设计支柱是**信息不对称与欺骗**（侦查可信度、假情报、AI 信任度系统），而非传统 RTS 的堆兵平推；改动玩法时不得削弱这一核心循环（庙算 → 战役实时 → 战后叙事）。

## 设计约定（实现时需遵守的文档事实）

- 兵种循环克制（v1.1 定案，旧 v1.0 环已废弃）：枪兵/矛阵 → 轻骑 → 弩兵/弓兵 → 重步 → 枪兵，克制 ×1.35 / 被克 ×0.75（GDD §2.4.1）。
- 诡道技能是**数据驱动**的（谋略点消耗 + 双层冷却，部分全局仅一次，如"焚粮断道"），新技能应走配置而非硬编码；欺骗类技能效果=向情报系统注入假情报对象（篡改载荷而非扣可信度）。
- AI 架构：情报模块 → 信任度系统（按主题分桶、有符号更新、桶=无方向的当前载荷采信度，欺骗需先预养桶再反转载荷）→ 目标优先级矩阵 → 行为树/Utility AI → 适应模块（GDD §3.1）；ML-Agents 不进决策主链路（P1-3 待正式合入，当前 §5.1 已注明方向）。情报/信任度的全部数值以情报规格 v1.2（边界闭合批）为唯一真源。
- 时间：所有系统必须声明挂战略层（旬/回合制）或战役层（秒/实时，1 日=6 战场分钟，单局 15–20 分钟封顶），禁止无归属的"回合/日"。
- 存档：JSON 本地/Steam Cloud；历史事件以可回放"编年史"保存（§5.4）。
- 目标平台：PC（Steam）优先，主机/移动端为后续。

## Unity/C# 工程约定（2026-09-10 初始化，P2-5 落地）

- **工程结构**：Unity 6 LTS 工程即仓库根（`ProjectSettings/ProjectVersion.txt` 钉 `6000.0.83f1`，changeset `dacc44548933`）。代码在 `Assets/Scripts/`，测试在 `Assets/Tests/`（EditMode/NUnit）。Library/Temp 等生成物已忽略，二进制资源走 Git LFS（见 `.gitattributes`）。
- **asmdef 分层（依赖只能向下，禁止反向/环）**：
  - `ChinaBettle.Foundation`：纯 C#、**零 UnityEngine 依赖**（`noEngineReferences: true`），承载全部核心规则公式——可信度、信任桶/门控、幕僚代理信号（TRUST-10/12）、结阵反骑与火区结算（COMBAT-17/SKILL-03）、克制环/伤害/士气、双层时钟、补给饥饿、技能数据定义、目标优先级、AI 欺骗参数。**权威端与未来服务端共用此层。**
  - `ChinaBettle.Time / Units / Intel / Stratagems / AI / Battle / Game`：依次向上，可用 UnityEngine；`Game` 为最顶层引导。
  - 改公式先改 Foundation 纯逻辑，在 `Assets/Tests/` 加/改 EditMode 测试锁定真源算例（已锁定：fresh75、aged68、§4.1 预养链路、桶 0.586/0.20、异质信源识破、痕迹 10 分钟不刷新、克制环、伤害保底、谋略点经济、反思负反馈 ×0.5、幕僚信号三档与性格偏移、结阵移速 ×0.8/反噬 6 点、火区 8 点/2 秒、AI 欺骗 0.7 倍率与 30 点/90s）。
- **语言级别与 API 面（C# 9 / .NET Standard 2.1；2026-09-11 全量修复后定型）**：Unity 6 的脚本语言级别是 **C# 9**、API 面是 **.NET Standard 2.1**。下列写法在桌面 `dotnet`（默认 latest）能过、在 Unity 里必炸，**一律禁用**：
  - `file-scoped namespace`（`namespace X;`，C# 10）→ 用块式 `namespace X { }`。
  - `record struct`（C# 10）→ 手写 `readonly struct` + `IEquatable<T>` + `==/!=`（参照 `Scripts/Foundation/Map/MapPoint.cs`、`Scripts/Intel/IntelRecord.cs` 的 `IntelPayload`）。
  - `required` 成员（C# 11）→ 用对象初始化器；代价是 `Nullable enable` 下会报 CS8618（已知权衡，勿用无依据 `!` 压制）。
  - `Random.Shared`（.NET 6）→ 自持 `private static readonly Random`（见 `Scripts/Foundation/Trust/AdvisorSignal.cs`）。
  - `Enumerable.MaxBy/MinBy`（.NET 6）→ `OrderByDescending(...).FirstOrDefault()`（见 `Scripts/Foundation/AI/GoalPriority.cs`）。
  - `init` 访问器是 C# 9、**允许用**，但它依赖 `System.Runtime.CompilerServices.IsExternalInit`，而该类型不属于 .NET Standard 2.1。已由 `Scripts/Foundation/IsExternalInit.cs` **公开**声明一次（Foundation 被其余所有程序集引用，故一处足够；改回 `internal` 会让 Intel/AI/Battle/Game 全部 CS0518）。升到 C# 10+ 或 .NET 5+ 后必须删掉该文件，否则 CS0101。
  - **命令行闸门**：`.tmp-dotnet/ChinaBettle.Cs9Check.csproj`（`netstandard2.1` + `LangVersion 9.0`，gitignored）编译 Foundation/Intel/Units/Stratagems/AI/Battle 纯逻辑源码，`dotnet build` 即可在不开 Unity 时拦住上述问题。**Game / Editor / Tests 三个程序集依赖 UnityEngine 或 Unity 定制 NUnit，闸门覆盖不到，只有 Unity batchmode 能验证。**
  - Unity 的 NUnit 是定制版 3.5（`com.unity.ext.nunit` 2.0.5），**没有 `Assert.Multiple`**（官方 NuGet NUnit 有，所以命令行闸门察觉不到）——测试里不要用。
  - 跑 EditMode 测试：`Unity.exe -batchmode -nographics -projectPath <根> -logFile <.tmp-dotnet/unity-tests.log> -runTests -testPlatform EditMode -testResults <.tmp-dotnet/editmode-results.xml>`。**不要加 `-quit`**——它会连测试一起跳过、且不产出 `results.xml`；只有「仅编译不跑测试」时才配 `-quit`。
  - 排错顺序：先统计错误码分布（`grep -o "error CS[0-9]*" <log> | sort | uniq -c`）。**上层程序集的错误会被下层编译失败掩盖**——Foundation 一报错，Game/Editor/Tests 的错误就都不显示，必须逐层修到 0 才会暴露下一层，别把某一轮的报错数当成全部。
- **数值纪律（与设计文档真源表同等强制）**：
  - 禁止在逻辑代码里写魔法数字。所有阈值/CD/倍率/权重经 `*Config` record（`CredibilityConfig`/`TrustConfig`/`CombatConfig`/`SupplyConfig`/`StrategyPointConfig`/`DeceptionDetectionConfig`）注入，默认值必须等于真源表并在注释标注真源 ID（INTEL-/TRUST-/COMBAT-/FOOD-/SKILL-/DECP-）。
  - 改数值顺序：先改《关键数值与规则真源表》→ 同步 Config 默认值与相关测试 → 再改引用文档。
  - 欺骗技能篡改系数（×0.3/×1.5）是 `StratagemDefinition` 配置数据（DECP-01），新技能走配置而非硬编码；SKILL-13 标注的 5 个草案技能（反间/声东/断粮/伪传/坚壁）禁止实现（枚举上有 `[Obsolete]`）。
- **情报边界（NET-02/SLICE-06，原型就分开）**：`IntelRecord` 只有接收方可见字段；真值与欺骗元数据在独立的 `AuthoritativeIntel`（单机=本地权威侧，未来=服务端）。任何认知查询只走 `IntelPool`，禁止代码直接读敌方真值。
- **时钟纪律**：所有计时必须声明 `TimeLayer`（战略=旬/回合，战役=秒/实时）；战役逻辑一律吃 `BattleClock` 时间，暂停由时钟冻结承载，不读 Unity `Time.time` 做规则结算。
- **C# 风格**：见 `.editorconfig`（Allman、PascalCase 公共成员、私有 `_camelCase`）；语言级别固定为 **C# 9**（见上「语言级别与 API 面」，务必先读再写代码）、`init`+`record`（class）表达不可变数据；nullable 目前仅有注解、**Unity 侧未开启 `#nullable` 上下文**（会报 CS8632 提示，非错误）；禁止吞异常、禁止 `as any` 式强制（C# 中为禁止无依据 `!`/禁用警告压制）。
- **AI 开发环境**：WSL 侧 dotnet 8 SDK 在 `~/.dotnet`（需 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`，已写入 `~/.bashrc`），C# LSP = csharp-ls 0.16，项目级配置在 `opencode.json`。Foundation 纯逻辑可不打开 Unity 直接用 `dotnet` 验证；Unity 编译/EditMode 测试在 Windows 侧用 Unity 编辑器 batchmode 跑（路径见 `.omo/` 下安装脚本，编辑器装于 `D:\Unity\Hub\Editor`）。

## Unity 工程脚手架（已落地，2026-09-10 增补）

- **`.meta` 文件已纳入仓库**：所有 `Assets/` 下脚本、asmdef、文件夹均带 `.meta`；脚本走 `MonoImporter`，asmdef 走 `AssemblyDefinitionImporter`，文件夹走 `DefaultImporter`。新增资产务必**一并提交对应 `.meta`**（防止不同克隆者生成不同 GUID 破坏资产引用）。
  - **文件**（.cs / .asmdef / 场景）的 GUID 走 `md5(归一化相对路径)` 约定（生成器 `.tmp-dotnet/gen_meta.py`，gitignored），**跨克隆一致**——这是资产引用不坏的关键。
  - **目录**的 GUID 由 Unity 首次导入时**随机分配**，本仓库**不保证**跨克隆一致；目录 GUID 不被资产引用，故无影响。**目录 meta 必须放在 `X.parent/X.meta`**（不是 `X/X.meta`）。
  - **历史坑（2026-09-11 已清理）**：旧版生成器误写成 `dirpath/meta_name`，往每个目录里再塞一份 `X/X.meta`，Unity 读不到便另生成了同级随机 GUID 的 meta，仓库里积下 27 个错位 meta + 27 个同名空目录（空目录 git 根本不跟踪，全新 clone 会得到 27 个无主 meta）。已删除，并把生成器改对。
  - 自检：`.tmp-dotnet/check_meta.py` 校验「目录 meta 是否缺失 / 是否有孤儿 meta / 文件 GUID 是否符合 md5 约定 / 是否有同名空目录残渣」，全 0 才算健康。
- **Editor 程序集 `ChinaBettle.Editor`**：`Assets/Editor/`，Editor-only 平台，引用 Foundation+Game，仅放 Editor 工具（菜单/校验/批处理），严禁放运行时逻辑。`ChinaBettleProjectSetup.cs` 是 `[InitializeOnLoad]` 入口，负责：启动日志（报告 Unity 版本与 `GameScope` 锚点常量）+ 首次入栈自动把 `Assets/Scenes/MainScene.unity` 挂到 `EditorBuildSettings` 索引 0。
- **启动场景 `Assets/Scenes/MainScene.unity`**：Unity 6 文本 YAML 格式最小骨架（`OcclusionCullingSettings`+`RenderSettings`+`LightmapSettings`+`NavMeshSettings`，无 GameObject），作为垂直切片接入点；后续 GameObject / MonoBehaviour 接线都在该场景内追加，禁止另起同名场景。
- **`ProjectSettings/`**：仅提交最小集——`ProjectVersion.txt`（钉 `6000.0.83f1`）+ `ProjectSettings.asset`（钉 companyName=`ChinaBettle`、productName=`战国·兵者诡道`，serializedVersion 28）+ `EditorBuildSettings.asset`（含 MainScene）。其余 `TagManager.asset` / `DynamicsManager.asset` / `QualitySettings.asset` 等仍由 Unity 首启按默认生成，**禁止手工改写**，且已由 `.gitignore` 忽略（`ProjectSettings/` 只放行上述 3 个文件，避免每次开 Unity 都把工作区弄脏）——后续如需自定义 Tag/Layer，**先**经真源表审批后由 Editor 工具批量写入，**届时再从 `.gitignore` 单独放行并提交该文件**。
- 当前 MVP 代码仅为**可编译骨架 + 公式实现 + 测试 + 工程脚手架**；不含场景内 GameObject / Prefab / MonoBehaviour 接线 / ScriptableObject 数据资产——这些归垂直切片迭代补，不属本批。

## 文档工作流

- 设计文档放 `Docs/`，**一律使用中文文件名**并带版本号（如 `游戏设计文档_v1.0.md`、`情报与可信度系统详细规格_v1.0.md`）；路线图条目用 Markdown checkbox 跟踪（见 GDD §6）。
- 当前从属文档：`基础兵种数值表_v0.3.md`（切片输入）、`首发12计技能列表_v1.2.md`（新增 5 计含反间计均为机制草案，火攻已定效果）。
- 改动已实现的玩法/数值时，先改 `关键数值与规则真源表_v1.4.md`，再同步引用文档并升版本号；代码中的伪代码（C#）仅为示意，不代表最终实现。
