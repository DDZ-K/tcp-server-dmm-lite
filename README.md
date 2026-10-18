# TCP Server 精简版 v1.0.1（MES + PCIe-N1616）

Windows x86、自包含 .NET 8 WinForms。程序与窗口图标为黄色幽灵，主界面 BOSCH 品牌区未改。

[下载 v1.0.1 ZIP](https://github.com/DDZ-K/tcp-server-dmm-lite/releases/download/v1.0.1/TcpServerDmmLite-v1.0.1.zip)

## 本次清理

- 删除不可用的 `Spe_PartReceived_Multi`、`Spe_PartProcessed_Multi`，以及对应无用辅助实现。
- 删除旧 `Spe_CH367_Input`、`Spe_CH367_Output`；IO 仅保留 `Spe_N1616_Input`、`Spe_N1616_Output`。
- 发送删除的命令返回未知命令 `-888`，不进入 MES/IO。
- 其他 MES 入口未改；`Spe_PartProcessed_MultiToSingle` 保留独立实现，仍需现场验证。
- 官方 N1616 SDK 依赖的 CH367DLL.dll 与板卡驱动仍需保留，不能因删除旧命令而删库。

## 快速开始

1. 解压整个 ZIP，保持 exe 与所有运行文件在同一目录，双击 `TcpServerDmmLite/TcpServerDmmLite.exe`。
2. 编辑 `TcpServerDmmLite/configure/Station.ini`。公开包 MES server 为安全占位地址 `127.0.0.1`，必须改为工位 MES 地址；端口按现场配置。默认设备 TCP 端口 60000，板卡 index=0。
3. 首次使用 IO 先安装包内微软 VC++ 2010 x86 运行库 `vcredist_x86.exe`，以及官方 N1616/CH367 驱动。自包含仅免装 .NET，不免驱动和原生运行库。

## 新 IO 指令

```text
Spe_N1616_Output|1 1
Spe_N1616_Output|1 0
Spe_N1616_Input|1
Spe_N1616_Input|
```

通道 1–16，输出电平 0/1；通道与电平之间是空格。单点输出只改对应位；整口输出会改全部 16 路。回包为对应内容，失败 `-888`。

完整参数与 MES 指令见 [docs/COMMANDS-v1.0.1.md](docs/COMMANDS-v1.0.1.md)。

## 验证与边界

- 清理前回归测试按预期失败（4 个待删除入口仍在）；清理后自测 SELFTEST_OK，退出码 0，注册命令 16 个（14 MES + 2 IO）。
- 实际发布 EXE 的 TCP 60000 已验证：4 个删除的命令与未知命令回 -888，Spe_MES_Init 回 1；测试程序已关闭。
- 无实卡和现场 MES 网络，真实 IO 与 MES 往返未验。不得视为产线验收，不含 PPV/Oracle，也未迁移 PLC、VISA、串口等外围。
