# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

桌面番茄钟应用，C# WinForms 编写，编译为独立 .exe 运行。无需浏览器，无外部依赖，依赖 Windows 自带的 .NET Framework。

## 文件说明

- `PomodoroTimer.cs` — 完整源码
- `PomodoroTimer.exe` — 编译后的可执行文件

## 编译命令

使用 .NET Framework 自带的 csc.exe 编译：

    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:PomodoroTimer.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll PomodoroTimer.cs

## 运行方式

双击 `PomodoroTimer.exe` 即可运行。支持键盘快捷键：空格开始/暂停，R 重置，→ 跳过。
