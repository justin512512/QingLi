# 恢复 Windows 系统时钟

若轻历异常退出后 Windows 时钟没有恢复，请在轻历目录运行：

```powershell
.\QingLi.Recovery.exe --restore-clock
```

该工具不启动轻历界面，也不需要管理员权限。它优先按轻历保存的快照恢复；快照缺失或损坏时会删除当前用户范围内的 `HideClock` 值，让系统时钟重新显示，并明确提示原策略无法自动还原。
