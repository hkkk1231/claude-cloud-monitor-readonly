# Claude 云端监控 · 只读监控开源项目

查看自己的 Linux 服务器性能、Claude 账号套餐及剩余额度。界面为中文，时间按北京时间显示。此工具由独立开发者制作，非 Anthropic 官方客户端。

## 首次使用

1. 把整个 ZIP 解压到自己有写入权限的文件夹，不要在压缩包内直接运行。
2. 双击 `ClaudeMonitor.exe`，填写自己的服务器 IPv4 地址或域名、SSH 用户名、端口和本机 SSH 私钥路径。包内没有预填连接或账号。
3. 双击 `验证SSH连接.cmd`。首次连接请通过服务器控制台或管理员核对 SSH 指纹，再确认信任。不要接受无法核对或发生变化的指纹。该脚本只检查连接、Python 和账号池前提，不安装软件或修改服务器配置。
4. 如果私钥有口令，先用 Windows OpenSSH 的 ssh-agent 加载自己的密钥；后台监控不能弹出私钥口令输入框。可在 PowerShell 执行 `ssh-add <自己的私钥路径>`。验证脚本最后还会检查后台无交互认证是否可用。
5. 再次打开 `ClaudeMonitor.exe`。更改连接请关闭当前窗口，再运行 `修改连接.cmd`，保存后重新打开。

## 运行前提

- Windows 10 1809 或更新版本、64 位系统、.NET Framework 4.8、Microsoft Edge WebView2 Runtime、Windows OpenSSH Client。缺少组件时使用 Windows 可选功能或 Microsoft 官方安装渠道；包内包含 WebView2 SDK 的运行依赖 DLL，不包含 WebView2 Runtime 安装器。
- 自己拥有访问权限的 Linux 服务器，SSH 密钥认证及 Python 3.8+ 可用，不需要 root 或 sudo。性能统计通过 `/proc` 与根分区读取。
- 账号额度依赖服务器用户目录 `~/.local/bin/cswap`，需要支持 `cswap list --json` 的 schemaVersion 1，以及 `cswap run <编号> -- auth status`。服务器应已安装 Claude Code 并以该用户完成账号登记和官方登录。参考 claude-swap 项目：<https://github.com/realiti4/claude-swap>。本包不分发或自动安装 claude-swap、Claude CLI，也不提供账号或登录缓存。
- 没有兼容账号池时，服务器性能仍可显示，账号区域显示不可用；不会虚构额度。

## 数据与操作边界

这是只读首页版：额度每60秒刷新、性能每10秒采样，支持暂停；最小化暂停自动采样，关闭取消本程序的查询。不含旧版终端、对话控制、飞书通知、每日巡检、自动回收、自动换号或制作任务。

连接只保存到本目录新生成的 `config.json`（服务器、用户名、端口、私钥路径）。私钥只由本机 OpenSSH 使用，不被复制到本目录或上传。账号凭据在服务器本地使用；SSH 只返回显示必需的邮箱、套餐、额度与性能字段。程序不向开发者发送遥测；邮箱和服务器地址会出现在使用者自己的界面中。显示结果仅存于运行内存，WebView2 的运行缓存位于使用者的 `%LOCALAPPDATA%\ClaudeMonitor\DashboardRuntime`；主机信任由使用者的 OpenSSH known_hosts 保存。

首次信任由使用者确认；日常监控严格验证 SSH 主机身份。SSH 不加载用户 SSH config，避免隐式跳板、转发或自动命令；本版仅支持直连，需跳板的环境暂不支持。查询不会启动 Claude 对话、发提示词、切账号或恢复任务。

错误、未知、历史缓存及重置后的待更新状态会如实标示。账号额度包含该账号的全部会话；到达预计重置时间不表示已经恢复。

## 转发与校验

请直接转发原始 ZIP。自己配置并运行后，不要把整个使用目录再次打包给别人；其中新产生的 `config.json` 属于自己的连接信息，诊断模式也可能产生自己的显示数据。发布包没有账号、密钥、浏览器资料、使用记录或测试截图。

GitHub Release 附带压缩包 SHA-256 校验文件。程序未做代码签名，Windows 可能显示未知发布者。保留 `vendor` 目录中的第三方许可。


## 从源码构建

在 Windows PowerShell 中运行 `.\build.ps1`，然后运行 `.\test.ps1`。生成文件位于 `bin/`；首次使用按上方步骤配置自己的连接。构建仅从 NuGet 下载固定版本 WebView2 SDK（1.0.4258.31），并核验 SHA-256；无需 Node.js 或本机 Claude CLI。GitHub Actions 同样执行构建和离线自测。`.deps/`、`bin/` 及使用者配置均不提交。测试不会连接服务器或发送 Claude 消息。

## 项目与许可

本项目为独立社区工具，与 Anthropic 无隶属关系。项目源码采用 MIT；第三方组件适用各自许可，见 THIRD_PARTY_NOTICES.md。

配套项目：[旧版交互](https://github.com/hkkk1231/claude-cloud-monitor-interactive) · [只读监控](https://github.com/hkkk1231/claude-cloud-monitor-readonly)。
