# AGENTS.md

本文件为 AI 编码助手提供本项目（RimWorld Mod **Smarter Capture Them**）的工作指南。

## 项目概述

RimWorld 1.5 / 1.6 的功能型 Mod，基于 Mlie 复活的 *Capture Them* 分支。核心功能：

- 新增工作类型 `CaptureThemCapture`（介于 Bed Rest 与 Basic 之间），殖民者会自动去抓捕被标记的、倒地的外阵营人形生物。
- 新增反向设计器（Designator）供玩家标记目标。
- 对倒地目标的抓捕前处理做了可配置扩展：原版 Tend、CE 的 Stabilize、[RH2] CPERS: Arrest Here 的原地逮捕、First Aid。

`About/About.xml` 中 `packageId` 为 `lke.Smarter.CaptureThem`，依赖 `brrainz.harmony`。

## 目录结构

| 路径 | 说明 |
| --- | --- |
| [Source/Capture Them/](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them) | C# 源码（`CaptureThem.csproj`） |
| [Source/Capture Them/WorkGiver_CapturePrisoners.cs](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/WorkGiver_CapturePrisoners.cs) | 工作分配核心：`WorkGiver_CapturePrisoners` 及其 FirstAid / CE 变体 |
| [Source/Capture Them/Designator_CapturePawn.cs](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/Designator_CapturePawn.cs) | 标记工具：`Designator_CapturePawn` 及其 FirstAid / CE 变体 |
| [Source/Capture Them/HarmonyPatches/](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/HarmonyPatches) | Harmony 补丁 |
| [Source/Capture Them/StartUp.cs](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/StartUp.cs) | Mod 入口、Harmony 注册、Mod 检测、`SmartCaptureThemSettings` |
| [Defs/](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Defs) | `WorkGivers.xml`、`Designations.xml` |
| [Patches/](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Patches) | 针对其他 Mod 的 XML 兼容补丁 |
| [Languages/](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Languages) | 翻译（`Keyed/Keys.xml`、`DefInjected/`） |
| `1.4/` `1.5/` `1.6/Assemblies/` | 各版本编译输出目录，构建产物直接写入这里 |

## 构建

工作目录：`Source/Capture Them`，使用 .NET SDK（`net48` 目标框架）。

```powershell
dotnet build "CaptureThem.csproj" -c v16
```

配置说明（见 [CaptureThem.csproj](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/CaptureThem.csproj)）：

- `v16`：输出到 `1.6/Assemblies/`，定义常量 `v16`，直接引用游戏目录下的 `Assembly-CSharp.dll` / `UnityEngine.CoreModule.dll`（路径 `..\..\..\..\RimWorldWin64_Data\Managed\`，即 RimWorld 根目录）。
- `v15` / `v14` / `Debug`：通过 NuGet 包 `Krafs.Rimworld.Ref` 提供引用，`v15` 输出到 `1.5/Assemblies/`，`v14` 输出到 `1.4/Assemblies/`。
- `Release` 输出到 `1.6/Assemblies/`。

> 配置名必须写成合法的 C# 标识符（`v14`/`v15`/`v16`，不要写 `1.4`/`1.5`/`1.6`）：MSBuild 会无条件把配置名当作预处理器符号传给编译器，带 `.` 的名字会被转成非法的 `1_5` 并产生 `warning MSB3052`。

版本差异统一用 `#if v16` / `#else` / `#endif` 条件编译处理（例如 `VacuumUtility` 仅在 1.6 可用、`DraggableDimensions` 仅在 1.5 及以下需要）。

## 代码约定

- 命名空间：主体使用 `SmartCaptureThem`；历史遗留的补丁类使用 `Capture_Them.HarmonyPatches`，新增代码请沿用所在文件既有命名空间。
- Def 引用统一集中在 [CaptureThemDefOf.cs](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/Source/Capture%20Them/CaptureThemDefOf.cs)（`[DefOf]`），不要散落 `DefDatabase.GetNamed`；跨 Mod 的 JobDef（`CP_FirstAid`、`Stabilize`、`CP_ImprisonInPlace`）因不在本 Mod 中定义，才在 `StartUp` 中按需 `DefDatabase<JobDef>.GetNamed` 并缓存到静态字段。
- 变体类通过继承 + `protected new DesignationDef Designation` 覆写实现（FirstAid / CE 三套），新增变体请沿用该模式。
- 调试日志统一用 `if (StartUp.settings.debug)` 包裹，输出前缀 `[Smarter Capture]`。
- 新增/修改 UI 设置项时，需同时更新 `SmartCaptureThemSettings` 字段、`ExposeData()` 中的 `Scribe_Values.Look`，以及 `StartUp.DoSettingsWindowContents`。
- 玩家可见文本一律走 `.Translate()`，对应 key 写入 `Languages/*/Keyed/Keys.xml`。

## 重要约束

- **不要主动删除代码中正在使用的注释**，除非用户明确要求。
- 改动 `Designator` / `WorkGiver` 的匹配条件时，三套变体（默认、FirstAid、CE）逻辑目前高度重复，修改一处时需同步检查其余两处。
- `About/About.xml` 的 `supportedVersions` 与 `modVersion` 需与发布版本保持一致。
- 构建产物（`*.dll`）会写入版本目录，不要提交到版本库之外的位置；`obj/`、`.vs/`、`bin/` 已在 `.gitignore` 中忽略。
- 不要新建文档类文件（README、说明文档等），除非用户明确要求。

## 提交规范

沿用仓库现有风格：祈使句、首字母大写的简短英文摘要，例如：

```
Fix "Collection was modified" error
Added support for [RH2] CPERS: Arrest Here!
```

改动用户可见行为时同步更新 [About/Changelog.txt](file:///d:/Program%20Files%20(x86)/Steam/steamapps/common/RimWorld/Mods/SmarterCaptureThem/About/Changelog.txt)。
