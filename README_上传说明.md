# Sidebar 开源代码上传说明

## 📦 文件准备

本目录已包含以下文件：
- ✅ LICENSE - GPL v3 许可证
- ✅ README.md - 项目说明
- ✅ AUTHORS - 作者信息
- ✅ .gitignore - Git 配置
- ✅ 上传指南.md - 详细上传步骤

## ⚠️ 重要：需要手动复制源代码文件

由于技术限制，源代码文件（.cs）需要手动复制。请执行以下步骤：

### 方法一：使用批处理文件（推荐）

1. 双击运行 `复制源代码.bat`
2. 等待复制完成
3. 检查文件是否都已复制

### 方法二：手动复制

1. 打开项目根目录：`C:\Users\zbfzb\Documents\projects\Sidebar\`
2. 复制以下文件到本目录：
   - 所有 `.cs` 文件（如 SidebarForm.cs, DesktopForm.cs 等）
   - `Sidebar.csproj`
   - `Sidebar.sln`
   - `CUSTOM_EFFECTS_README.md`（如果存在）
   - `DEVELOPMENT.md`（如果存在）

## 🚀 上传到 GitHub

### 最简单的方法：网页上传

1. **访问 GitHub**：https://github.com/new
2. **创建新仓库**：
   - Repository name: `Sidebar`
   - Description: `基于 ShareX 开发的 Windows 侧边栏应用程序`
   - 选择 **Public**（必须公开，符合 GPL v3）
   - 不要勾选任何初始化选项
3. **点击 "Create repository"**
4. **上传文件**：
   - 在仓库页面点击 **"uploading an existing file"**
   - 将本目录的所有文件拖拽到上传区域
   - 填写提交信息：`Initial commit: Sidebar open source release`
   - 点击 **"Commit changes"**

### 使用 Git 命令行

```bash
# 1. 进入本目录
cd "C:\Users\zbfzb\Documents\projects\Sidebar\开源协议上传完整代码"

# 2. 初始化 Git
git init

# 3. 添加所有文件
git add .

# 4. 提交
git commit -m "Initial commit: Sidebar open source release"

# 5. 在 GitHub 创建仓库后，添加远程仓库
git remote add origin https://github.com/您的用户名/Sidebar.git

# 6. 推送代码
git branch -M main
git push -u origin main
```

## 📋 上传前检查清单

- [ ] 所有 `.cs` 源代码文件已复制
- [ ] `Sidebar.csproj` 和 `Sidebar.sln` 已复制
- [ ] `LICENSE` 文件存在
- [ ] `README.md` 文件存在且内容完整
- [ ] `AUTHORS` 文件存在
- [ ] 所有文件中的版权声明完整

## ⚖️ GPL v3 合规要求

根据 GPL v3 许可证要求：

1. ✅ **必须公开源代码** - 所有源代码必须可访问
2. ✅ **必须包含 LICENSE** - 已包含完整的 GPL v3 许可证
3. ✅ **必须保留版权声明** - 所有文件都包含版权信息
4. ✅ **必须说明基于项目** - README 中已说明基于 ShareX

## ⚠️ 隐私信息说明

**本开源代码已移除所有隐私信息！**

为了遵守开源协议并保护隐私，以下信息已被替换为占位符：
- 邮箱地址：`1780555120@qq.com` → `your-email@example.com`
- API 服务器：`https://auth.hudiege.cn` → `https://your-api-server.com`

详细说明请查看 `PRIVACY_NOTICE.md` 文件。

## 📧 联系方式

- **作者**：蝴蝶哥
- **反馈**：请通过 GitHub Issues 或 Pull Requests 提交问题

## 🎉 完成

上传完成后，您的项目将：
- 符合 GPL v3 开源协议要求
- 可以被其他开发者使用和贡献
- 保留您的版权声明
- 明确说明基于 ShareX 开发

祝上传顺利！

