# AnyTimeSave 兼容说明

本副本只改动 AnyTimeSave 冲突相关的存档身份判断。

## 冲突原因

AnyTimeSave 在拍原生快照时会临时执行：

```text
PlayerStore.saveSlotId = 9001/9100
PlayerStore.SaveGame()
PlayerStore.saveSlotId = 原槽位
```

原实现把 `saveSlotId` 和 `runID` 一起作为状态信任条件。临时槽位出现时会被判定为跨档，从而清理 `psfw.*` 状态。

## 本副本改法

- 状态身份只使用 `runID`。
- 不再要求 `StateSaveSlot == 当前 saveSlotId`。
- 保留 `PlayerStore.LoadGame` 的内存缓存失效，确保 AnyTimeSave 恢复快照后会重新读取快照内的 `psfw.*`。
- 保留 `PlayerStore.StartNewGame` 的清理逻辑。