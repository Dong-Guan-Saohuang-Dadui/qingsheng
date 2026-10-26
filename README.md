# 轻声 QingSheng

只接讯飞「语音听写（流式版）」的 Windows 轻量工具。C# / WinForms，无第三方依赖，无模型。

## 使用

F5 开始录音，再按 F5 结束。也会在讯飞判断停顿结束后自动完成，最长录音 55 秒。
识别最终文字通过 Windows Unicode 键盘事件直接输入，不使用剪贴板。
默认只向开始录音时的窗口输入，切换窗口则保留结果并提示。
托盘右键 → 设置，或双击话筒图标。

## 设置

所有设置自动保存并即时应用，无需手动保存。底部显示成功或失败提示；数字停止输入约 350ms 后应用，凭据停止输入约 600ms 后加密保存。无效参数保留上次有效值，快捷键冲突自动恢复原值。

- 语音识别：停顿时间（500～10000ms）、自动标点、阿拉伯数字规范化、简体/香港繁体、普通话/英语/方言免切/指定方言。
- 方言免切和指定方言需先在讯飞开通授权；指定方言需填控制台的 accent 参数。没有授权会报错。
- 浮窗与输入：是否显示、空闲显示、实时文字、录音闪动、宽度、高度、屏幕 X/Y、不透明度、自动输入和窗口限制。
- 浮窗可以拖动移动，边缘或右下角拉伸。调整后自动保存几何信息；设置内可点击预览拖动，坐标和宽高会同步更新。
- 快捷键与启动：F1～F12，可搭配 Ctrl/Alt/Shift；开机自启动使用当前用户 Run 启动项，不需要管理员权限。默认不开启，开关修改后立即生效。
- 讯飞凭据：APPID、APIKey、APISecret；用 Windows DPAPI 按当前用户加密保存。

浮窗默认 260×64，位于主屏右下角。浮窗实时显示识别中的文字，最终结果才输入到光标，避免动态修正干扰输入框。
录音时托盘和浮窗话筒闪动；可在设置关闭闪动。浮窗不会抢键盘焦点。
关闭数字规范化不保证全部数字返回汉字表达，由引擎决定。
普通权限程序无法向部分管理员窗口输入；个别应用不支持模拟 Unicode 键盘事件。托盘「查看上次结果」可以查看识别文本。

## 配置与启动

配置目录：%LOCALAPPDATA%\XunfeiLite
- credentials.bin：DPAPI 加密凭据。
- options.json：语音、浮窗和输入设置。
- hotkey.json：快捷键。
开机自启动位置：HKCU\Software\Microsoft\Windows\CurrentVersion\Run\XunfeiLite。
移动 EXE 后请关闭再开启自启动选项。

双击 XunfeiLite.exe 启动；命令行 --settings 可在程序首次启动时显示设置。
需要 Windows 10/11 x64 和 .NET Framework 4.8。音频仅发送讯飞，不保存录音和历史，不读写剪贴板。

## 构建与验证

powershell -ExecutionPolicy Bypass -File build.ps1
XunfeiLite.exe --self-test
查看 self-test.txt。已覆盖参数映射、动态修正、Unicode 输入事件、配置序列化和屏幕外坐标恢复。
已人工检查设置页布局和保存。真实语音识别、方言授权和多种目标应用的输入兼容性仍需实际使用验证。

参考 SayType 功能思路，根据讯飞协议独立编写，没有复制该项目源码。
https://github.com/qwerty726/SayType
https://www.xfyun.cn/doc/asr/voicedictation/API.html


## 开源说明

MIT License。请使用自己的讯飞 API 凭据；本仓库不包含个人配置、密钥或录音。第三方语音服务受讯飞自身条款约束。

源文件保留最初的 XunfeiLite 命名；产品名称为轻声 QingSheng。

