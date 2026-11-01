# 中三武将管理工具（ZhongSanEditor）

把《中华三国志》里不想让他/她出场的武将**彻底下架**，并顺手清理所有跨表引用；
也可以当作一个轻量的剧本/存档字段编辑器用。

- 不需要改游戏 exe，不需要逆向汇编：游戏数据本来就是明文 JSON。
- 单文件 exe（需要 .NET 8 桌面运行时），双击即用。
- 所有写操作前自动备份，备份文件在 `D:\re\zhongsan\backup\`。

## 快速使用

1. 双击 `dist\ZhongSanEditor.exe`。
2. 菜单 **文件 → 打开存档**（`文档\WorldOfTheThreeKingdoms\Save\*.json`）或 **打开剧本**
   （`...\Release\Content\Data\Scenario\*.json`）。
3. 左侧筛选：性别选「女」、ID 段选「9000-24999 新增」、五维总分 ≥ 300，即可看到当前档里超模的那批。
4. 用规则按钮批量勾选（如「五维 ≥ 300 的女性」「9000 段全部女性」），再在表格里逐行微调：
   勾选 = 要下架；双击任意一行可以改这个人的所有字段。
5. 点底部红色按钮 **「应用屏蔽到当前文件」**，确认后写入。
6. 弹窗会列出改了什么、清理了哪些引用，并给出备份路径。

> 想同时改剧本（以后新开档也干净）：先勾好名单，再用 **工具 → 应用屏蔽到另一个文件…** 选 `Content\Data\Scenario\190FDLM.json` 之类的剧本。
> 名单也能用 **名单 → 导出/导入名单…** 存成 JSON，下次直接导入。

## 界面说明

| 区域 | 作用 |
|---|---|
| 左栏筛选 | 姓名/ID 搜索、性别、ID 段、状态（已登场/未登场/已勾选）、五维总分下限、只看女性 |
| 规则按钮 | 9000 段全部女性 / 五维 ≥ 阈值女性 / 五维 ≥ 300 女性 / 筛选结果勾选 / 取消 / 清空 |
| 中间表格 | 屏蔽、ID、姓名、性别、五维、五维分项、状态、生死、出场年、生年、卒年、世代、所在、势力、ID 段；点表头排序 |
| 统计 | 总人数、女性数、已登场数、已勾选数（其中已登场多少）、当前显示行数 |
| 底部按钮 | 应用屏蔽到当前文件 / 上一层 / 下一页 / 下一层 / 最后一页 / 查找分页 |

表格里的颜色：女性行淡粉底，已勾选行灰底，女性且五维 ≥ 300 的分数标红加粗。

## 命令行（可脚本化）

```
ZhongSanEditor.exe --file <剧本或存档.json> --ids 24,9372,9017 [--ids-file list.txt]
                   [--out 新文件] [--report 报告.txt] [--backup-dir 目录] [--force]
ZhongSanEditor.exe --file <json> --selftest [--report 报告.txt]
```

- `--ids`：要下架的武将 ID，逗号/空格/分号分隔；`--ids-file` 每行一个，`#` 开头是注释。
- `--out`：另存为新文件，不改原文件。
- `--force`：被屏蔽者是君主/储君/太守/主将时也继续（会自动改立君主/储君，清空太守与主将）。
- 退出码：`0` 成功，`1` 失败，`2` 因君主/主将冲突中止，`3` 参数错误。
- 报告默认写到 `<文件>.report.txt`（WinExe 没有控制台，只能看文件）。

## 「彻底下架」到底改了哪些字段

对每个被屏蔽的武将：

- 人物本体：`Alive = false`、`Available = false`（死亡 + 未登场，出场判定 `MeetAvailableCondition()` 不会再命中）。
- 城池：从 `PersonsString / MovingPersonsString / NoFactionPersonsString / NoFactionMovingPersonsString / feiziliebiaoString / CaptivesString` 里剔除；若是 `MayorID` 则置 -1。
- 部队：从 `PersonsString / CaptivesString` 剔除；若是 `LeaderIDString` 则清空。
- 俘虏：删除 `captiveData` 里以他为俘虏的记录。
- 宝物：`BelongedPersonIDString` 置 -1（**`HidePlaceIDString` 是城池 ID，不会动**）。
- 部队事件：`LaunchPersonString / EffectPersonsString / TargetPersonsString` 剔除。
- 人际关系：删除 `PersonRelationIds` 里涉及他的配对。
- 关系表：`SpouseIds / FatherIds / MotherIds / BrotherIds / HatedIds / CloseIds / SuoshuIds / MarriageGranterId`
  删除他本人的条目，并把别人对他的引用剔除或置 -1。
- 人物单值引用：`princessTakerID / waitForFeiziId / suoshurenwu / ConvincingPersonID` 置 -1。
- 势力：若他是君主/储君，自动改立势力内五维最高（优先原储君）的在任成员；没人可继则置 -1 并告警。

全程只改已有字段的值，不新增/删除字段、不打乱字段顺序，保证游戏的反序列化器仍然认得。

## 安全与回滚

- 每次写入前把原文件复制到备份目录：`<文件名>.bak.<yyyyMMdd-HHmmss>.json`。
- **工具 → 备份与恢复…** 可查看备份、一键恢复（恢复前会先把当前状态再备份一次）。
- 建议进游戏前先在游戏内存一个新档，万一异常可以回退。

## 已知限制

- 加密存档（设置里 `EncryptSave=True`，文件是 gzip）不支持，工具会直接提示；请在游戏设置里关掉加密存档。
- 改存档只影响当前进度；要影响新开档必须同时改对应剧本文件。
- 随机生成的新人物（ID ≥ 25000）每个档都不一样，屏蔽名单对它们没有意义。
- 数据写入是"游戏能读"级别的兼容，但没在真机进游戏验证过战斗/势力逻辑；改完第一次进游戏请留意君主继承与部队编成。

## 自检

```
dist\ZhongSanEditor.exe --file <存档> --selftest
```

会依次验证：读取、五维计算、各类筛选、在临时副本上真实执行屏蔽、回读校验（人物已死、城池/部队/宝物/俘虏/关系表无残留、数量不变）、重复屏蔽幂等。
2026-11-01 对 `Save00.json`（1454 人）与 `190FDLM.json`（1375 人）全部通过。

## 源码结构

```
tools/ZhongSanEditor/
  Program.cs            入口（有 "-" 参数走命令行，否则开界面；可传一个文件路径直接打开）
  Cli.cs                命令行参数与报告
  Core/Json.cs          JsonNode 读写小工具（只在字段已存在时改值，保序）
  Core/GameDocument.cs  载入/原子保存/容器访问（解析前照游戏的做法删掉 \r\n）
  Core/PersonRow.cs     人物行视图模型 + GameIndex（从城池/部队/俘虏反查所在与势力）
  Core/BlockService.cs  「彻底下架」的全部改动与报告
  Core/Services.cs      配置、备份、命令行应用流程
  Core/SelfTest.cs      --selftest 自检
  UI/MainForm.cs        主界面
  UI/PersonDetailForm.cs 单个人物全字段编辑
  UI/ReportForm.cs      报告窗口、备份恢复窗口
```

构建：`dotnet build -c Debug`；发布单文件：`dotnet publish -c Release -o ..\..\dist`。
