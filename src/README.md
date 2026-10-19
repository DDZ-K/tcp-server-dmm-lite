# TCP Server v1.0.1 VS2022 源码

打开 TcpServerDmmLite.sln，Release / x86，恢复 NuGet 后重新生成。VS2022 17.14.23 + SDK9.0.308 已实测。目标 net8.0-windows；需要 .NET 桌面开发工作负载。

本源码包没有厂内 MES 模板、历史响应报文、日志或内网地址。运行 MES 前把已有运行包的 Templates 复制到生成 exe 同目录；编译和 IO 不需要 MES XML。示例 MES 地址127.0.0.1必须按现场修改。

源码业务与 v1.0.1 相同，仅默认服务器地址与旧注释域名作安全替换。原工程不变。
