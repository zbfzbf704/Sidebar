# 自定义图片特效开发指南

## 概述

ShareX 的图片特效系统支持二次开发，你可以创建自己的自定义特效类。

## 架构说明

### 基类：`ImageEffect`

所有特效都必须继承自 `ImageEffect` 抽象类：

```csharp
public abstract class ImageEffect
{
    public bool Enabled { get; set; }  // 是否启用此特效
    public string Name { get; set; }   // 特效名称
    
    public abstract Bitmap Apply(Bitmap bmp);  // 必须实现：应用特效
    protected virtual string GetSummary() { return null; }  // 可选：返回摘要信息
}
```

## 创建自定义特效的步骤

### 1. 创建特效类

创建一个继承自 `ImageEffect` 的类：

```csharp
using ShareX.ImageEffectsLib;
using ShareX.HelpersLib;
using System.ComponentModel;
using System.Drawing;

namespace Sidebar.CustomEffects
{
    public class MyCustomEffect : ImageEffect
    {
        // 定义可配置的属性
        [DefaultValue(50)]
        [Description("特效强度 (0-100)")]
        public int Intensity { get; set; }

        // 构造函数：应用默认值
        public MyCustomEffect()
        {
            this.ApplyDefaultPropertyValues();
        }

        // 实现 Apply 方法：处理图片
        public override Bitmap Apply(Bitmap bmp)
        {
            using (bmp)
            {
                Bitmap result = (Bitmap)bmp.Clone();
                // 在这里实现你的特效逻辑
                // ...
                return result;
            }
        }

        // 可选：返回摘要信息（显示在特效列表中）
        protected override string GetSummary()
        {
            return $"强度: {Intensity}";
        }
    }
}
```

### 2. 属性特性说明

- `[DefaultValue(value)]`：设置属性的默认值
- `[Description("描述")]`：属性的描述信息（会在 UI 中显示）

### 3. 性能优化技巧

#### 使用不安全代码（unsafe）处理像素

对于需要逐像素处理的特效，使用 `unsafe` 代码可以大幅提升性能：

```csharp
public override Bitmap Apply(Bitmap bmp)
{
    using (bmp)
    {
        Bitmap result = (Bitmap)bmp.Clone();
        BitmapData bmpData = result.LockBits(
            new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)bmpData.Scan0;
            int bytesPerPixel = 4;
            int stride = bmpData.Stride;

            for (int y = 0; y < result.Height; y++)
            {
                byte* row = ptr + (y * stride);
                for (int x = 0; x < result.Width; x++)
                {
                    int index = x * bytesPerPixel;
                    // 处理像素：B, G, R, A
                    byte b = row[index];
                    byte g = row[index + 1];
                    byte r = row[index + 2];
                    byte a = row[index + 3];
                    
                    // 修改像素值...
                }
            }
        }

        result.UnlockBits(bmpData);
        return result;
    }
}
```

**注意**：需要在项目文件中启用 `AllowUnsafeBlocks`：

```xml
<PropertyGroup>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

#### 使用 ShareX 的工具类

ShareX 提供了很多有用的工具类：

- `ColorMatrixManager`：颜色矩阵操作（亮度、对比度、色调等）
- `ImageHelpers`：图片处理辅助方法（模糊、缩放等）

### 4. 示例特效

已提供 4 个示例特效在 `CustomImageEffects.cs` 中：

1. **CustomInvert**：颜色反转特效（可调节强度）
2. **CustomTint**：色调调整特效
3. **CustomMosaic**：马赛克特效
4. **CustomEdgeGlow**：边缘发光特效

### 5. 集成到项目

1. 将 `CustomImageEffects.cs` 添加到项目中
2. 确保项目已启用 `AllowUnsafeBlocks`
3. 编译项目
4. 在 ImageEffectsForm 中，你的自定义特效会自动出现在特效列表中

### 6. 特效分类

ShareX 的特效分为几个类别（仅供参考，自定义特效可以放在任何命名空间）：

- **Adjustments**：调整类（亮度、对比度、饱和度等）
- **Filters**：滤镜类（模糊、锐化、边缘检测等）
- **Drawings**：绘制类（背景、边框、文本等）
- **Manipulations**：操作类（裁剪、旋转、缩放等）

### 7. 注意事项

1. **资源管理**：始终使用 `using` 语句或手动 `Dispose()` 释放 `Bitmap` 资源
2. **线程安全**：`Apply` 方法可能在不同线程中调用，确保代码是线程安全的
3. **性能**：对于大图片，考虑使用多线程或异步处理
4. **错误处理**：在 `Apply` 方法中添加适当的错误处理

### 8. 调试技巧

- 使用 `GetSummary()` 方法返回当前参数值，方便在列表中查看
- 使用 `System.Diagnostics.Debug.WriteLine()` 输出调试信息
- 在 Visual Studio 中设置断点调试

## 参考资源

- ShareX 源码：`ShareX-develop/ShareX.ImageEffectsLib/`
- 现有特效示例：
  - `Adjustments/Brightness.cs`：简单的颜色调整
  - `Filters/Blur.cs`：模糊滤镜
  - `Drawings/DrawText.cs`：文本绘制

## 许可证

自定义特效代码需要遵循 GPL v3 许可证（与 ShareX 相同）。

