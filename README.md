# TCP Server 精简版（MES + PCIe-N1616）

Windows x86、自包含 .NET 8 WinForms 发布包。可执行文件和完整运行目录请从 **[Releases](https://github.com/DDZ-K/tcp-server-dmm-lite/releases)** 下载。

## 快速开始

1. 下载并解压 Release ZIP，保持 `TcpServerDmmLite.exe` 与其旁边文件在同一目录。
2. 编辑 `TcpServerDmmLite/configure/Station.ini`，将 `[MES] server` 改成当前工位的 MES 服务器地址。Release 包里的 `127.0.0.1` 只是安全占位地址，不是实际 MES 地址。
3. 首次用 IO 前安装包内 `vcredist_x86.exe` 和官方 N1616/CH367 驱动。
4. 双击 `TcpServerDmmLite.exe`。默认设备 TCP 监听端口为 60000。

程序图标为用户提供的黄色幽灵图案；主界面 BOSCH 品牌栏未改。

## 功能和验证边界

- 设备命令格式：`Spe_函数名|参数`；失败响应 `-888`。
- 不含 PPV/Oracle；依赖 PPV 参数持久化的配对板 Multi 命令会明确失败。
- 基础自测及本机 TCP 未知命令响应已验证；无 N1616 实卡或工位 MES 网络，真实硬件 IO 和 MES 往返仍需现场验收。
