# PLC 气密检测上位机

本项目是基于 C#、.NET 8 和 WinForm 的气密检测产线数据采集软件。扫码后按工位保存一条内存待检测记录，PLC 检测完成标志从 0 跳变为 1 时读取气密字符串，并按工位匹配条码后写入 SQLite。

主界面顶部显示PLC连接、PLC当前工位、当前产品和待检测数量。左侧“数据查询”支持按日期、工位、产品和条码筛选历史检测记录，并可导出当前查询结果。

## 开发环境

- Visual Studio 2022（安装“.NET 桌面开发”工作负载）
- .NET 8 SDK
- NuGet：SunnyUI 3.9.8、NModbus 3.0.83、Microsoft.Data.Sqlite 8.0.30、NLog 5.4.0

## 构建

```powershell
dotnet restore .\AirtightInspection.sln
dotnet build .\AirtightInspection.sln -c Release
dotnet publish .\AirtightInspection.WinForms\AirtightInspection.WinForms.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64-final
```

推荐部署目录：`publish\win-x64-final`。这是 .NET 8 自包含发布版本，目标机无需单独安装 .NET Desktop Runtime。

生成正式安装包：

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1
```

安装包输出到 `artifacts\installer`。程序按当前用户安装到本地应用目录，无需管理员权限，数据库、日志、配置和作业指导书目录均可正常写入。

## 启动和配置

1. 修改发布目录中的 `Config.ini`，至少核对 PLC IP、端口、从站号和寄存器地址。
2. 运行 `AirtightInspection.exe`。首次运行会自动建立 `Data\mydb.db`、`Logs` 和 `ProductManual`。
3. 默认管理密码为 `123456`，可在 `[Security] Password` 修改。
4. 进入“工位配置”新增并启用工位，再从主界面新增产品。
5. 键盘扫码模式下先点击主窗口再扫码；回车或换行会立即提交，Tab、GS 等字段分隔符会作为条码内容保留。未配置结束符时会在扫码输入结束约 400ms 后自动提交。串口模式需配置 COM 口参数。
6. 正式运行必须使用实际扫码设备输入条码；程序不提供二维码模拟测试入口。

## PLC 联调

- NModbus 使用从 0 开始的协议地址：D4000 对应配置地址 4000，而不是显示地址 44001。
- 标志和工位号均按两个 16 位保持寄存器读取。默认低字在前，可用 `WordOrder` 和 `ByteOrder` 调整。
- 气密字符串默认每寄存器两个 ASCII 字符、寄存器低字节在前、无长度头。
- PLC 应先写气密字符串和工位号，最后将标志从 0 置 1。
- 入库成功后上位机写 2，失败写 3；PLC 收到应答后需把标志清零。
- 正式联调必须连接实际 PLC，按上述顺序验证。标志持续为 1 时不会重复入库，必须先回到 0 才能形成下一次上升沿。

详细架构、接口约定和验收方式见 [气密检测上位机技术实施方案.md](./气密检测上位机技术实施方案.md)。
