# 开发指南

## 项目概述

本项目是基于 ShareX（GPL v3 许可证）开发的新应用程序。

## 开发环境要求

- **.NET SDK**: 9.0 或更高版本
- **IDE**: Visual Studio 2022 或 Visual Studio Code
- **操作系统**: Windows 10/11 (版本 10.0.22621.0 或更高)

## 项目结构

```
MyNewApp/
├── Program.cs              # 应用程序主入口
├── MyNewApp.csproj         # 项目配置文件
├── MyNewApp.sln            # 解决方案文件
├── LICENSE.txt             # GPL v3 许可证
├── README.md               # 项目说明
├── DEVELOPMENT.md          # 开发指南（本文件）
└── .gitignore              # Git 忽略文件配置
```

## 开始开发

### 1. ShareX 源代码路径

ShareX 源代码位于项目根目录下的 `ShareX-develop` 文件夹：

**路径**：`C:\Users\zbfzb\Documents\projects\Sidebar\ShareX-develop`

如果该目录不存在，你可以：

```bash
# 克隆 ShareX 仓库到项目根目录
cd C:\Users\zbfzb\Documents\projects\Sidebar
git clone https://github.com/ShareX/ShareX.git ShareX-develop
```

### 2. 添加项目引用

如果你需要使用 ShareX 的库（如 HelpersLib、ScreenCaptureLib 等），可以在 `Sidebar.csproj` 中添加项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="ShareX-develop\ShareX.HelpersLib\ShareX.HelpersLib.csproj" />
  <!-- 添加其他需要的库引用 -->
</ItemGroup>
```

**注意**：ShareX-develop 路径位于项目根目录：`C:\Users\zbfzb\Documents\projects\Sidebar\ShareX-develop`

### 3. 构建项目

```bash
dotnet build
```

### 4. 运行项目

```bash
dotnet run
```

或者在 Visual Studio 中按 F5 运行。

## GPL v3 许可证合规检查清单

在开发过程中，请确保遵循以下 GPL v3 要求：

### ✅ 必须完成的事项

1. **保留原始版权声明**
   - 所有基于 ShareX 的代码文件必须包含 ShareX 的版权声明
   - 在你的代码文件中添加你自己的版权声明

2. **使用 GPL v3 许可证**
   - 项目根目录必须包含 `LICENSE.txt`（完整的 GPL v3 文本）
   - 所有源代码文件应包含 GPL v3 许可证声明

3. **标注修改说明**
   - 在修改的文件中明确标注修改日期和修改内容
   - 在 README 中说明基于哪个项目开发

4. **提供源代码**
   - 分发应用时必须同时提供完整的源代码
   - 或者明确说明如何获取源代码

5. **UI 中的许可证信息**
   - 建议在"关于"对话框中显示许可证和版权信息
   - 显示基于 ShareX 的说明

### 📝 代码文件模板

每个源代码文件的开头应包含以下格式的许可证声明：

```csharp
#region License Information (GPL v3)

/*
    [你的应用名称] - [简要描述]
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    ---
    
    Based on ShareX:
    Copyright (c) 2007-2025 ShareX Team
    Licensed under GPL v3
    
    ---
    
    Copyright (c) 2025 [你的名字]
    This program is a derivative work based on ShareX.
    
    Modified on: [修改日期]
    Modifications: [简要说明修改内容]
*/

#endregion License Information (GPL v3)
```

## 添加新功能

1. 创建新的类文件时，记得添加 GPL v3 许可证声明
2. 如果是基于 ShareX 的代码修改，保留原始版权信息并添加你的修改说明
3. 提交代码前检查是否包含必要的许可证信息

## 依赖管理

### 使用 ShareX 的库

如果你要使用 ShareX 的库（如 `ShareX.HelpersLib`），需要：

1. 添加项目引用（推荐，如果 ShareX 在同一解决方案中）
2. 或编译 ShareX 库为 DLL 后引用（需要确保 DLL 也遵循 GPL v3）

### 添加第三方 NuGet 包

注意：使用第三方库时，需要确保它们的许可证与 GPL v3 兼容，或者你的应用可以合法地组合使用。

## 构建和发布

### Debug 构建

```bash
dotnet build -c Debug
```

### Release 构建

```bash
dotnet build -c Release
```

### 发布应用程序

```bash
# 发布为单文件
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 发布为可执行文件
dotnet publish -c Release -r win-x64
```

**重要**：发布应用时，必须同时提供源代码，或者明确说明如何获取源代码。

## 贡献代码

欢迎提交 Issue 和 Pull Request。请确保：

- 代码遵循 GPL v3 许可证要求
- 包含必要的版权和许可证声明
- 代码质量和风格符合项目规范

## 常见问题

### Q: 我可以将基于 ShareX 的应用商业化吗？

A: 可以，但你必须：
- 使用 GPL v3 许可证
- 提供完整的源代码
- 保留原始版权声明

### Q: 我可以修改代码而不开源吗？

A: 不可以。GPL v3 是 Copyleft 许可证，基于它的代码必须同样使用 GPL v3 开源。

### Q: 如何引用 ShareX 的代码？

A: 你可以：
- 直接复制代码（保留版权声明）
- 引用 ShareX 的项目（如果它们在同一解决方案中）
- Fork ShareX 并在此基础上开发

## 资源链接

- [GPL v3 许可证全文](https://www.gnu.org/licenses/gpl-3.0.html)
- [ShareX GitHub](https://github.com/ShareX/ShareX)
- [.NET 9.0 文档](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9)

## 获取帮助

如果对 GPL v3 许可证有疑问，建议：
1. 仔细阅读 GPL v3 许可证全文
2. 咨询知识产权律师（特别是商业项目）
3. 参考 FSF（自由软件基金会）的 FAQ

