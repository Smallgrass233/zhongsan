# 交接说明（换电脑继续用）

这个仓库是「中华三国志 1.23.1」的数据整理与改动工具集，**不含游戏本体，也不含游戏数据文件**（那些在游戏安装目录和「我的文档」里，体积大且受版权保护）。

## 一、新电脑需要准备什么

| 依赖 | 版本 | 用途 |
|---|---|---|
| .NET SDK | 8.0（含桌面运行时） | 编译 `tools/ZhongSanEditor` |
| Python | 3.10+（标准库即可） | 跑 `notes/*.py` 数据脚本 |
| 游戏本体 | 中华三国志 1.23.1 | 数据源 |
| dnSpyEx（可选） | 6.x | 改成长公式时打 IL 补丁 |

游戏数据默认位置（脚本里写死的路径，换机器要改脚本顶部的常量）：

- 数据表与剧本：`<游戏目录>\Content\Data\Common\CommonData.json`、`<游戏目录>\Content\Data\Scenario\*.json`
- 存档：`C:\Users\<你>\Documents\WorldOfTheThreeKingdoms\Save\*.json`

## 二、目录说明

```
tools/ZhongSanEditor/   C# WinForms 武将管理工具（源码 + 用法见其 README）
notes/                  所有脚本、方案文档、称号目录
  ├─ 玩法改造方案.md              ★ 总方案（资质/巅峰/称号/技能/人才稀缺）
  ├─ 武将成长与特性获取机制.md     机制说明（各工作加什么属性、称号怎么来）
  ├─ 女性武将清理与新增记录.md     已做的清理与新增记录
  ├─ 称号成就候选目录.html/.csv   ★ 891 个待改造称号，勾选用
  ├─ build_title_catalog.py      生成上面两份目录
  ├─ nerf_female_traits.py       女性特性清理（已执行）
  ├─ add_women.py                新增 20 位女性（已执行）
  ├─ delete_titles.py            删除 29 个异常称号（已执行）
  └─ dedupe_titles.py            同名称号合并（只演练过，未执行）
dist/                   发布好的 ZhongSanEditor.exe
patches/                dnSpy 补丁工程（目前为空）
```

`backup/`、`ilspy/` 不进仓库：前者是每次改数据前的大体积备份，后者是反编译产物。到新机器后如果要回滚，只能从旧机器的 `backup/` 里拷，或者用游戏原始的 `CommonData.json`/存档。

## 三、已完成（2026-11-01）

1. **武将管理工具 v1**：可视化筛选 + 勾选屏蔽，彻底下架（Alive=false、清城池/部队/俘虏/宝物/关系引用），命令行与自检；`dist/ZhongSanEditor.exe`。
2. **女性特性清理**：44 个数据文件（26 剧本 + 20 存档），按规则删幻想级称号、裁技能/特技、压五维到 330、魅力到 95。
3. **新增 20 位女性**：ID 9700–9719，写进 44 个文件的 Persons + AllBiographies。
4. **删除 29 个异常称号**（恶搞/穿越/妖术），母表 + 12 个内嵌副本，引用清理干净。
5. 文档：玩法改造方案、机制说明、清理记录、称号目录 v3.1（含 CSV）。

## 四、下次要做的事（按优先级）

1. **同名称号合并**（用户已确认要做，说好下次执行）
   - `python notes/dedupe_titles.py` 看计划：108 组同名、删 132 个、234 处人物引用要改指。
   - 规则：优先保留 ID < 1302 的手写正牌，其次 Lv 高、ID 小；引用改指存活条目。
   - 确认后加 `--apply`，会自动备份到 `backup/titles-dedupe-<时间>/`。
2. **称号条件落库**：等用户在 `称号成就候选目录.csv` 里勾选保留项，然后按 `玩法改造方案.md` 3.3 的 Lv 分级表写入 `ConditionTableString` + `AutoLearn`。
3. **资质 + 巅峰**（方案见 2.5 节）：需要新增 `AptitudeString` 字段并改进经验/上限公式，属于 IL 层改动，dnSpy 或重编译；数据侧只能先做“五维上限”近似。
4. **人才稀缺参数**：方案 5 节已列参数表，等用户拍板。
5. **事件解锁史诗武将**：用户说先不做。

## 五、改数据前的规矩

- 先关游戏（存档是磁盘文件，游戏退出时覆盖）。
- 脚本默认只演练，加 `--apply` 才写；写前自动备份到 `backup/`。
- 每次改完用游戏自己的序列化器回读验证：`mod/test/LoadTest.exe <json> <游戏目录>`，或跑 `ZhongSanEditor.exe --selftest <file>`。
- 称号改动必须同时改「母表 + 每个数据文件内嵌的 GameCommonData 副本」，只改母表对已开存档无效。
