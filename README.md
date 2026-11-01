# 中三（中华三国志 1.23.1）逆向改造工程

目标：对 `D:\app\中三\中华三国志1.23.1-1.24.1版本\Release` 下的单机游戏做逆向分析与改造。

## 结论先行：这是 .NET (C#) 游戏，不是原生二进制

| 事实 | 证据 |
| --- | --- |
| 引擎 | MonoGame + SharpDX，`.NET Framework 4.8`（见 `WorldOfTheThreeKingdoms.exe.config`） |
| 主程序 | `WorldOfTheThreeKingdoms.exe` 2.69 MB 托管程序集 |
| 符号 | 附带 `WorldOfTheThreeKingdoms.pdb`（7 MB）——可直接源码级调试 |
| 资源 | `Content/` 2.97 GB，贴图是普通 **PNG**（不是 xnb 包） |
| 数据 | `Content/Data/*.xml`、`Scenario/*.json`，改数值/文案不用碰代码 |
| 模组 | 自带 `MODs/<id>/` 覆盖机制（`MODs/1/1.txt` 清单 + Data/Textures） |

所以**不需要 Ghidra 啃汇编**，走 C# 路线即可；x64dbg / Ghidra 只在这一层解决不了时再上。

## 三条改造路线（按性价比排序）

1. **数据 Mod（首选）**：改 `Content/Data/*.xml`、`Scenario/*.json`、`Textures/Resources/*.png`，或做成 `MODs/<新id>/` 覆盖包。零代码风险，可随时删。
2. **IL 打补丁**：用 dnSpy 直接改 `WorldOfTheThreeKingdoms.exe` 里的方法（改完 File→Save Module），逻辑类改动走这条。
3. **重编译**：`ilspy/` 里已经是完整可读 C# 工程（1661 个文件 + .csproj），长期维护可以考虑，但依赖 MonoGame 与内容管线，成本高。

## 本目录结构

```
D:\re\zhongsan\
├── backup\           原版 exe/pdb/config + 用户存档快照（改坏了回滚用）
├── ilspy\            反编译出的完整 C# 工程（ILSpy 8.2，DOTNET_ROLL_FORWARD=Major）
├── decompile\        dnSpy 控制台的反编译产物（当时卡住，可删）
├── notes\            分析笔记
├── mod\              改造中的 MOD 工作副本
└── patches\          打过补丁的 exe 副本（原版永远不覆盖）
```

## 武将管理工具（已可用）

`dist\ZhongSanEditor.exe`（源码 `tools\ZhongSanEditor\`）—— 可视化勾选「不想让其出场的武将」，一键把人物置死并从所有城池/部队/俘虏/宝物/人际关系里清除引用；剧本与存档都支持，写入前自动备份。

用法见 `tools\ZhongSanEditor\README.md`，需求与实现记录见 `notes\需求对齐-武将屏蔽工具.md`。

## 数据清理与武将机制文档

- `notes\武将成长与特性获取机制.md` —— **五维怎么练、各项内政/军事工作分别加什么属性、称号/技能/特技的五条获取路径、成功率公式、可调参数速查**（本次查阅的目标文档）。
- `notes\女性武将特性目录.html` —— 清理前全部女性武将的称号/技能/特技明细，可排序、搜索、展开效果全文。
- `notes\女性武将清理与新增记录.md` —— 已执行的清理规则、统计、新增 20 位女性名单与备份回滚方式。
- `notes\nerf_female_traits.py` / `notes\add_women.py` —— 对应脚本（均支持 `--dry` 预览）。

## 存档位置（改造前已备份）

- 存档：`D:\Documents\WorldOfTheThreeKingdoms\Save\Save*.json`（19 个，共 170 MB）
- 设置：`D:\Documents\WorldOfTheThreeKingdoms\Setting.config`
- 快照：`backup\userdata\`

还原：把 `backup\userdata\Save` 和 `Setting.config` 拷回 `D:\Documents\WorldOfTheThreeKingdoms\`。

## 已装工具

| 工具 | 位置 | 用途 |
| --- | --- | --- |
| dnSpyEx 6.6.0 | `D:\tools\dnspy\dnSpy.exe` | 反编译 + 改 IL + 附加调试（有 pdb，体验很好） |
| dnSpy.Console | `D:\tools\dnspy\dnSpy.Console.exe` | 命令行批量反编译 |
| ILSpy CLI 8.2 | `~\.dotnet\tools\ilspycmd.exe` | 需要 `DOTNET_ROLL_FORWARD=Major`，秒级导出可编译工程 |
| .NET SDK 8.0.427 | `C:\Program Files\dotnet` | 编 C# 补丁/工具 |
| Ghidra 12.2 / x64dbg | `D:\tools\ghidra`、`D:\tools\x64dbg` | 备用：原生层 / 运行时内存 |
| Python venv | `D:\tools\re\venv` | capstone/pefile/lief，脚本处理 PE |

## 关键代码入口（反编译工程里）

- `WorldOfTheThreeKingdoms\MainGame.cs` — 主循环、异常兜底
- `WorldOfTheThreeKingdoms.GameScreens\MainGameScreen.cs` — 存档/读取/自动存档（6490 行附近）
- `GameManager\Setting.cs`、`Platforms\Platform.cs` — 用户目录、MOD 选择
- `GameObjects\GameScenario.cs` — 剧本数据结构与存读
- `MainMenuScreen.cs:523` — MOD 扫描与加载（`MODs` 目录）
- `PluginInterface\*.cs` — 30 个内置 UI 插件接口

## 安全约定

- 原版 exe 只读：所有修改产物进 `patches\`，需要时替换并保留原名备份。
- 单机游戏，改数值/剧情随意；不碰联机与反作弊，不分发游戏本体。
- 动手前先关游戏，避免文件占用；改完先跑一局新开档验证。
