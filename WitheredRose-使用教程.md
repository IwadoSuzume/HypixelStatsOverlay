# WitheredRose 使用教程

Hypixel 起床战争战绩查询 + 游戏内悬浮窗工具，自动读取本局名单并把战绩悬浮显示在游戏画面上。

**下载**：https://suzume.cloud/downloads/WitheredRose-Setup.exe（双击安装，无需管理员权限）

## 快速上手

1. 前往 developer.hypixel.net 获取 API Key（复制后切回软件会自动弹窗询问是否使用）
2. "Player Lookup" 页填写：API Key、自己的 ID、Minecraft 日志路径（一般自动检测好）
3. 点击 **Save Settings & Start Watching**

## 功能

- **Stat Card**：自动显示你的战绩，30 秒刷新一次
- **Player Lookup**：调节悬浮窗透明度/大小；AutoWho（开局自动发送 /who）
- **Window Tools**：选中游戏窗口，一键去边框/无边框全屏
- **游戏内**：按住 Tab 显示悬浮窗；FINAL KILL 自动移除被淘汰玩家；回大厅/切服自动清空名单
- 标题栏"托盘"按钮 = 最小化到后台；右上角 X = 真正退出
- 右上角可切换中/英文界面

## 常见问题

- **悬浮窗不显示**：游戏需为窗口化或无边框全屏，独占全屏下无法显示（Window Tools 页一键转换）
- **查询超时/失败**：服务器在国外，网络不稳定可尝试代理
- **提示找不到玩家**：对方开启了昵称隐藏
- **重置设置**：删除 `%APPDATA%\HypixelStatsOverlay\config.json`
