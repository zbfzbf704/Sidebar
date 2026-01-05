#region License Information (GPL v3)

/*
    自定义图片特效示例 - 展示如何扩展 ShareX 图片特效功能
    
    基于 ShareX ImageEffectsLib 开发
    Copyright (c) 2007-2025 ShareX Team
    Licensed under GPL v3
    
    ---
    
    Copyright (c) 2025 蝴蝶哥
    Email: your-email@example.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion License Information (GPL v3)

using ShareX.ImageEffectsLib;
using ShareX.HelpersLib;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;

namespace Sidebar.CustomEffects
{
    /// <summary>
    /// 示例1：简单的颜色反转特效
    /// </summary>
    public class CustomInvert : ImageEffect
    {
        [DefaultValue(100)]
        [Description("反转强度 (0-100)")]
        public int Intensity { get; set; }

        public CustomInvert()
        {
            this.ApplyDefaultPropertyValues();
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            using (bmp)
            {
                Bitmap result = (Bitmap)bmp.Clone();
                float intensity = Intensity / 100f;

                // 锁定位图数据以提高性能
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
                            
                            // BGR 格式（注意：Bitmap 使用 BGR 而不是 RGB）
                            byte b = row[index];
                            byte g = row[index + 1];
                            byte r = row[index + 2];
                            byte a = row[index + 3];

                            // 反转颜色
                            row[index] = (byte)(b + (255 - b) * intensity);     // B
                            row[index + 1] = (byte)(g + (255 - g) * intensity); // G
                            row[index + 2] = (byte)(r + (255 - r) * intensity); // R
                            // Alpha 通道保持不变
                        }
                    }
                }

                result.UnlockBits(bmpData);
                return result;
            }
        }

        protected override string GetSummary()
        {
            return $"强度: {Intensity}%";
        }
    }

    /// <summary>
    /// 示例2：自定义色调调整特效
    /// </summary>
    public class CustomTint : ImageEffect
    {
        [DefaultValue(0)]
        [Description("色调值 (0-360)")]
        public int Hue { get; set; }

        [DefaultValue(50)]
        [Description("饱和度 (0-100)")]
        public int Saturation { get; set; }

        public CustomTint()
        {
            this.ApplyDefaultPropertyValues();
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            using (bmp)
            {
                // 使用 ShareX 的颜色矩阵工具
                return ColorMatrixManager.Hue(Hue).Apply(
                    ColorMatrixManager.Saturation(Saturation / 100f).Apply(bmp)
                );
            }
        }

        protected override string GetSummary()
        {
            return $"H:{Hue}° S:{Saturation}%";
        }
    }

    /// <summary>
    /// 示例3：自定义马赛克特效
    /// </summary>
    public class CustomMosaic : ImageEffect
    {
        [DefaultValue(10)]
        [Description("马赛克块大小 (像素)")]
        public int BlockSize { get; set; }

        public CustomMosaic()
        {
            this.ApplyDefaultPropertyValues();
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            using (bmp)
            {
                Bitmap result = (Bitmap)bmp.Clone();
                int blockSize = BlockSize.Max(2);

                // 锁定位图数据
                BitmapData srcData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                BitmapData dstData = result.LockBits(
                    new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.Scan0;
                    byte* dstPtr = (byte*)dstData.Scan0;
                    int bytesPerPixel = 4;

                    for (int y = 0; y < bmp.Height; y += blockSize)
                    {
                        for (int x = 0; x < bmp.Width; x += blockSize)
                        {
                            // 计算当前块的边界
                            int blockEndX = (x + blockSize).Min(bmp.Width);
                            int blockEndY = (y + blockSize).Min(bmp.Height);

                            // 计算块的平均颜色
                            long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
                            int pixelCount = 0;

                            for (int by = y; by < blockEndY; by++)
                            {
                                byte* srcRow = srcPtr + (by * srcData.Stride);
                                for (int bx = x; bx < blockEndX; bx++)
                                {
                                    int index = bx * bytesPerPixel;
                                    sumB += srcRow[index];
                                    sumG += srcRow[index + 1];
                                    sumR += srcRow[index + 2];
                                    sumA += srcRow[index + 3];
                                    pixelCount++;
                                }
                            }

                            // 计算平均值
                            byte avgB = (byte)(sumB / pixelCount);
                            byte avgG = (byte)(sumG / pixelCount);
                            byte avgR = (byte)(sumR / pixelCount);
                            byte avgA = (byte)(sumA / pixelCount);

                            // 填充整个块
                            for (int by = y; by < blockEndY; by++)
                            {
                                byte* dstRow = dstPtr + (by * dstData.Stride);
                                for (int bx = x; bx < blockEndX; bx++)
                                {
                                    int index = bx * bytesPerPixel;
                                    dstRow[index] = avgB;
                                    dstRow[index + 1] = avgG;
                                    dstRow[index + 2] = avgR;
                                    dstRow[index + 3] = avgA;
                                }
                            }
                        }
                    }
                }

                bmp.UnlockBits(srcData);
                result.UnlockBits(dstData);
                return result;
            }
        }

        protected override string GetSummary()
        {
            return $"{BlockSize}px";
        }
    }

    /// <summary>
    /// 示例4：自定义边缘发光特效
    /// </summary>
    public class CustomEdgeGlow : ImageEffect
    {
        [DefaultValue(3)]
        [Description("边缘检测阈值 (0-255)")]
        public int Threshold { get; set; }

        [DefaultValue(5)]
        [Description("发光半径 (像素)")]
        public int GlowRadius { get; set; }

        [DefaultValue(255)]
        [Description("发光颜色 - 红色分量 (0-255)")]
        public int GlowR { get; set; }

        [DefaultValue(255)]
        [Description("发光颜色 - 绿色分量 (0-255)")]
        public int GlowG { get; set; }

        [DefaultValue(255)]
        [Description("发光颜色 - 蓝色分量 (0-255)")]
        public int GlowB { get; set; }

        public CustomEdgeGlow()
        {
            this.ApplyDefaultPropertyValues();
        }

        public override Bitmap Apply(Bitmap bmp)
        {
            using (bmp)
            {
                // 创建边缘检测图
                Bitmap edgeMap = EdgeDetect(bmp, Threshold);
                
                // 应用模糊以创建发光效果
                ImageHelpers.BoxBlur(edgeMap, GlowRadius);
                
                // 将发光效果叠加到原图
                using (edgeMap)
                {
                    return BlendGlow(bmp, edgeMap);
                }
            }
        }

        private Bitmap EdgeDetect(Bitmap bmp, int threshold)
        {
            Bitmap result = new Bitmap(bmp.Width, bmp.Height);
            
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
            }

            BitmapData srcData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;
                int bytesPerPixel = 4;

                for (int y = 1; y < bmp.Height - 1; y++)
                {
                    for (int x = 1; x < bmp.Width - 1; x++)
                    {
                        // Sobel 边缘检测
                        int gx = GetPixelIntensity(srcPtr, srcData.Stride, x + 1, y) -
                                 GetPixelIntensity(srcPtr, srcData.Stride, x - 1, y);
                        int gy = GetPixelIntensity(srcPtr, srcData.Stride, x, y + 1) -
                                 GetPixelIntensity(srcPtr, srcData.Stride, x, y - 1);
                        
                        int magnitude = (int)System.Math.Sqrt(gx * gx + gy * gy);
                        
                        if (magnitude > threshold)
                        {
                            int index = y * dstData.Stride + x * bytesPerPixel;
                            dstPtr[index] = 255;     // B
                            dstPtr[index + 1] = 255; // G
                            dstPtr[index + 2] = 255; // R
                            dstPtr[index + 3] = 255; // A
                        }
                    }
                }
            }

            bmp.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private unsafe int GetPixelIntensity(byte* ptr, int stride, int x, int y)
        {
            int index = y * stride + x * 4;
            // 计算灰度值
            return (int)(ptr[index] * 0.114 + ptr[index + 1] * 0.587 + ptr[index + 2] * 0.299);
        }

        private Bitmap BlendGlow(Bitmap original, Bitmap glow)
        {
            Bitmap result = (Bitmap)original.Clone();

            BitmapData origData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData glowData = glow.LockBits(
                new Rectangle(0, 0, glow.Width, glow.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData resultData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* origPtr = (byte*)origData.Scan0;
                byte* glowPtr = (byte*)glowData.Scan0;
                byte* resultPtr = (byte*)resultData.Scan0;
                int bytesPerPixel = 4;

                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        int index = y * origData.Stride + x * bytesPerPixel;
                        
                        // 获取发光强度（使用 alpha 通道）
                        float glowIntensity = glowPtr[index + 3] / 255f;
                        
                        // 混合原图和发光效果
                        resultPtr[index] = (byte)(origPtr[index] + (GlowB - origPtr[index]) * glowIntensity * 0.5f);     // B
                        resultPtr[index + 1] = (byte)(origPtr[index + 1] + (GlowG - origPtr[index + 1]) * glowIntensity * 0.5f); // G
                        resultPtr[index + 2] = (byte)(origPtr[index + 2] + (GlowR - origPtr[index + 2]) * glowIntensity * 0.5f); // R
                        resultPtr[index + 3] = origPtr[index + 3]; // A
                    }
                }
            }

            original.UnlockBits(origData);
            glow.UnlockBits(glowData);
            result.UnlockBits(resultData);
            return result;
        }

        protected override string GetSummary()
        {
            return $"阈值:{Threshold} 半径:{GlowRadius}px";
        }
    }
}

