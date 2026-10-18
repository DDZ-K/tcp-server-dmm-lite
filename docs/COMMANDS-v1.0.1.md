# v1.0.1 触发指令

TCP 默认端口 60000，ASCII；请求格式 `指令名|参数`。MES 参数用 `|` 分隔，IO 通道和电平用空格分隔。成功回包为内部成功返回的第二段，失败或未知命令返回 `-888`。一次请求等待一次响应，不把多条请求合成一条发送。

## IO：仅保留 N1616

- `Spe_N1616_Output|1 1`：第 1 路输出置 1。
- `Spe_N1616_Output|1 0`：第 1 路输出置 0。
- 通道 1–16，电平 0/1；单点输出不清其他输出。成功回 `1`。
- `Spe_N1616_Output|0001`：整口十六进制输出。
- `Spe_N1616_Output|0000000000000001`：整口 16 位二进制输出（高位在前）。注意整口命令会覆盖全部 16 路。
- 继承原 N1616 解析规则：单参数长度不超过 6 按十六进制，更长按二进制；建议整口只用 4 位十六进制或 16 位二进制。
- `Spe_N1616_Input|1`：读取第 1 路输入，正常回 `0` 或 `1`。
- `Spe_N1616_Input|`：读取全部输入，回 16 位状态对应的十进制整数。

置 1/0 不等于承诺继电器吸合/释放，须按实际接线验证。保留官方 SDK 所需的 CH367DLL.dll 和驱动，不再提供 CH367 命令别名。

## MES：14 个入口

下列尖括号为占位说明，发送时替换，不包含尖括号。模板路径沿用原工位实际值；某些模板路径包含 XML 文件名，不能一律简化为目录名。

```text
Spe_MES_Init|<服务器>|<端口>
Spe_PartReceived|<条码>|<模板路径>
Spe_PartReceivedwithDMC|<条码>|<模板路径>
Spe_PartReceivedOnlyDMC|<条码>|<模板路径>
Spe_PartReceivedParameter|<条码>|<模板路径>|<XPath> <属性名>
Spe_PartReceived_DMCnTracking|<条码>|<模板路径>
Spe_PartProcessed|<条码>|<模板路径>|<状态值>
Spe_PartProcessed_MultiToSingle|<条码>|<模板路径>|<状态值>
Spe_PCBAtoHousing|<PCBA码>|<模板路径>|<XPath> <属性名>
Spe_PCBAtoHousingWithTracking|<PCBA码>|<模板路径>|<XPath> <属性名>
Spe_PCBAtoDevice|<PCBA码>|<第一模板路径>|<XPath> <属性名>|<第二模板路径>
Spe_PCBAtoDeviceWithTracking|<PCBA码>|<第一模板路径>|<XPath> <属性名>|<第二模板路径>
Spe_ChangeOver|<条码或料号>|<模板路径>
Spe_MaterialChangeStarted|<条码或料号>|<模板路径>
```

- `PartReceived`、`PartReceivedwithDMC`、`PartReceivedOnlyDMC` 可追加 `|<参数名>`，默认 serialNumber。
- `PartProcessed` 还支持 `|<名称>|<值>` 或 `|<field>|<名称>|<值>`，多组字段的内部列表沿用 `@` 分隔。状态值沿用 MES 模板约定，勿猜为 OK/NG。
- `Spe_MES_Init` 仅改当前进程配置，不保存 Station.ini。
- `MultiToSingle` 是仍有具体 MES 实现的独立上报入口，本次未删除、未修改其业务；无现场验收，不代表整套多联板流程恢复。

## v1.0.1 删除的入口

- Spe_PartReceived_Multi
- Spe_PartProcessed_Multi
- Spe_CH367_Input
- Spe_CH367_Output

以上入口已从注册表和实现中删除，不是保留一个永远失败的接口；发送它们返回未知命令 `-888`，不会进入 MES/板卡调用。已删除对应无用 Multi 辅助实现。

旧工位脚本迁移：`Spe_CH367_Input|1` 改为 `Spe_N1616_Input|1`；`Spe_CH367_Output|1 1` 改为 `Spe_N1616_Output|1 1`。

## 验证范围

命令注册表、自测、真实发布 EXE 的本机 TCP 未知命令与 MES 初始化已验证。N1616 实卡输入输出和现场 MES 往返未验，不能视为产线验收。
