# 购买天赋通用兼容方案

## 目标

让 PerkShopFramework 在商店中购买天赋时，执行该天赋原本的开局/激活逻辑：

- 原版天赋走原版处理。
- ExtraPerks、WagesPerks、PPerkPack 等 MOD 天赋走各自提供者逻辑。
- 客人类型、店铺吸引力、交易倍率等持续型效果只依赖 `StartingPerk.IsPerkActive()`。
- 不再为具体天赋手写数值效果表。

## 已扫描的提供者

### 原版

来源：`StartingPerkContent.HandleNewGamePerk()`、`NewGameData.HandleInitialItem()`。

原版可购买天赋不手写静态表，运行时从 `StartingPerkList.Perks` 读取。

### ExtraPerks

已确认 10 个中文特性：

- 董事会内线
- 深度睡眠
- 卡车上掉的货
- 夜猫子
- 惹毛安保
- 拾荒者之王
- 势利眼
- 丑陋店铺
- 奇葩人物
- 人脉广泛

ExtraPerks 通过 `StartingPerkContent.HandleNewGamePerk()` 的 Harmony Postfix 调用
`CustomStartingPerks.NotifyNewGame()`。

### WagesPerks

已确认 22 个中文特性：

- 酒商之友
- 命运之骰
- 狄仁杰之手
- 博士之友
- 蛙哥牛逼
- 人神共愤
- 捡漏直觉
- 精神错乱
- 刀尖舔血
- 好酒之徒
- 童叟无欺
- 笑面虎
- 招贼体质
- 霉运缠身
- 信誉扫地
- 治安部眼线
- 流浪者
- 蛙娘
- 声名狼藉
- 退休枪匠之友
- 水商之友
- 王尔德之手

WagesPerks 的部分一次性内容挂在 `GameMaster.NewGame`，通过
`CustomStartingPerks.NotifyNewGame()` 执行；持续效果由各自 `IsPerkActive()` 判断。

### PPerkPack

已确认 4 个中文特性：

- 垃圾场挖掘者
- 夜猫子+
- 强健体魄
- 鉴定之神

PPerkPack 通过 `StartingPerkContent.HandleNewGamePerk()` 补丁执行自定义内容。

### FutureTech

- 未来科技

FutureTech 不属于通用 `StartingPerkContent` 路径，必须保留显式 Provider。

## 效果分类

### A. 持续查询型

只在运行时调用 `StartingPerk.IsPerkActive(id)`，不需要购买时执行一次性内容。

典型类别：

- 客人类型变化
- 店铺吸引力
- 声望获取倍率
- 交易倍率
- 每日概率
- UI/状态显示

处理方式：

```text
加入 startingPerks
不执行额外内容
由所属 MOD/原版持续查询 IsPerkActive
```

### B. 原版一次性 NewGame 内容

由 `StartingPerkContent.HandleNewGamePerk()` 处理。

处理方式：

```text
隔离为单天赋列表
调用原版 HandleNewGamePerk()
恢复原列表
写 AppliedContent 标记
```

### C. MOD 补丁型一次性内容

MOD 自己 Harmony Patch 了 `StartingPerkContent.HandleNewGamePerk()`。

处理方式：

```text
与 B 共用单天赋隔离调用
补丁会看到当前只有刚购买的 MOD 天赋处于 active
```

### D. MOD 独立初始化型

MOD 使用自己的 `NotifyNewGame()` 或 `OnNewGame()`，不一定挂在
`StartingPerkContent.HandleNewGamePerk()` 上。

处理方式：

```text
通过显式 Provider 调用所属 MOD 的公开/精确初始化入口
不复制效果数值
```

### E. 物品发放型

优先调用所属 MOD 或原版自己的创建/发放方法。

只有没有提供者入口时，才使用目录创建和背包路由兜底。

### F. 特殊系统型

例如空间站鲁滨逊、商人周期、机器、UI 事件等。

处理方式：

```text
只把天赋加入 startingPerks
由所属 MOD 的既有 IsPerkActive/生命周期补丁接管
PerkShopFramework 不重写业务系统
```

## 通用执行架构

### 1. Provider 接口

```csharp
internal interface IPurchasedPerkContentProvider
{
    string Name { get; }
    bool CanHandle(string perkId);
    bool TryApply(PlayerStore store, StartingPerk perk);
}
```

### 2. Provider 类型

- `NativeUnifiedProvider`
  - 隔离单天赋并调用 `StartingPerkContent.HandleNewGamePerk()`。
  - 覆盖原版和挂在同一 Harmony 入口上的 MOD。

- `WagesPerksProvider`
  - 精确调用 WagesPerks 自己的 `CustomStartingPerks.NotifyNewGame()`。
  - 不复制声望、物品或机器数值。

- `FutureTechProvider`
  - 调用 FutureTech 的注册/发放入口。
  - 保留现有明确适配，不进入手搓通用表。

- `ActiveOnlyProvider`
  - 仅登记 `startingPerks`，不执行一次性内容。
  - 适用于持续查询型机制。

- `ExplicitProvider`
  - 用于没有公开统一入口、但能明确调用初始化方法的 MOD。

### 3. 购买事务

购买时只执行：

```text
验证点数/互斥
加入 startingPerks
扣除点数
写 PendingContent.<perkId>
保存
排队执行内容效果
```

不要在购买按钮回调中直接执行原生内容。

### 4. 待处理队列

每个存档保存：

```text
psfw.PendingContent.<perkId> = 1
psfw.AppliedContent.<perkId> = 0
```

执行成功后：

```text
PendingContent = 0
AppliedContent = 1
```

### 5. 安全执行时机

必须满足：

- 有有效存档。
- 天赋面板已关闭。
- 没有内容应用正在进行。
- 没有 UI 场景切换。
- 一次只执行一个天赋。
- 最好延迟到购买后的下一帧或 `BeginDay` 安全阶段。

### 6. 单天赋隔离

```csharp
var original = store.startingPerks;
var isolated = new Il2CppPerkList();
isolated.Add(perk);

store.startingPerks = isolated;
IsApplyingPurchasedContent = true;

try
{
    provider.TryApply(store, perk);
    WriteInt(store, "AppliedContent." + id, 1);
    WriteInt(store, "PendingContent." + id, 0);
}
finally
{
    store.startingPerks = original;
    IsApplyingPurchasedContent = false;
}
```

## 禁止继续使用的做法

- 不维护 `天赋 -> 声望数值` 手写表。
- 不根据名字猜 Give/Grant/Create 方法。
- 不在按钮点击回调中直接执行整套 NewGame 内容。
- 不用 `BeginDay` 扫描所有已有天赋补效果。
- 不重写 RobinCrusoe 等完整机制系统。

## 验收矩阵

每个天赋至少验证：

1. 建档选择时原版正常。
2. 商店购买后只执行一次。
3. 跨日不重复执行。
4. 读档不重复执行。
5. 客人类天赋会改变客人类型。
6. 吸引力天赋会改变吸引力结果。
7. 声望天赋应用正确数值。
8. 事件/机器/商人类天赋进入所属 MOD 生命周期。