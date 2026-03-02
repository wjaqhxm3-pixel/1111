using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenSealWindows.Models;

namespace ScreenSealWindows.Services;

/// <summary>
/// Applies mosaic filter effects to captured screen regions using GDI+.
/// Equivalent to the macOS Core Image FilterProcessor.
/// </summary>
public sealed class FilterProcessor
{
    public static readonly FilterProcessor Shared = new();

    private FilterProcessor() { }

    /// <summary>
    /// Apply the specified mosaic filter to the given bitmap.
    /// </summary>
    public Bitmap ApplyFilter(Bitmap source, MosaicType type, double intensity, Color solidColor)
    {
        if (intensity <= 0) return (Bitmap)source.Clone();

        return type switch
        {
            MosaicType.Pixelation => ApplyPixelation(source, Math.Max(2, (int)intensity)),
            MosaicType.GaussianBlur => ApplyBoxBlur(source, Math.Max(1, (int)intensity)),
            MosaicType.RetroGaming => ApplyRetroGaming(source, (int)intensity),
            MosaicType.JpegCompression => ApplyJpegCompression(source, (int)intensity),
            MosaicType.SolidColor => ApplySolidColor(source, solidColor, intensity),
            _ => source
        };
    }

    /// <summary>
    /// Fills the bitmap with a specified solid color applying intensity as opacity.
    /// </summary>
    private Bitmap ApplySolidColor(Bitmap source, Color color, double intensity)
    {
        var result = (Bitmap)source.Clone();
        using (var g = Graphics.FromImage(result))
        {
            // Map intensity (0-100) to Alpha (0-255)
            int alpha = (int)Math.Clamp(intensity * 255 / 100, 0, 255);
            using var brush = new SolidBrush(Color.FromArgb(alpha, color));
            g.FillRectangle(brush, 0, 0, result.Width, result.Height);
        }
        return result;
    }

    /// <summary>
    /// Pixelation: scale down then scale back up with nearest-neighbor interpolation.
    /// </summary>
    private Bitmap ApplyPixelation(Bitmap source, int pixelSize)
    {
        int w = source.Width;
        int h = source.Height;
        int smallW = Math.Max(1, w / pixelSize);
        int smallH = Math.Max(1, h / pixelSize);

        var small = new Bitmap(smallW, smallH);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.Low;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.DrawImage(source, 0, 0, smallW, smallH);
        }

        var result = new Bitmap(w, h);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(small, 0, 0, w, h);
        }
        small.Dispose();
        return result;
    }

    /// <summary>
    /// Approximation of Gaussian blur using multiple box blur passes.
    /// </summary>
    private Bitmap ApplyBoxBlur(Bitmap source, int radius)
    {
        // Clamp to reasonable bounds
        radius = Math.Min(radius, 50);
        var bmp = (Bitmap)source.Clone();

        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        int stride = data.Stride;
        int byteCount = stride * bmp.Height;
        byte[] pixels = new byte[byteCount];
        byte[] buffer = new byte[byteCount];

        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, byteCount);

        // 3 passes of box blur for Gaussian approximation
        for (int pass = 0; pass < 3; pass++)
        {
            // Horizontal pass
            BoxBlurHorizontal(pixels, buffer, bmp.Width, bmp.Height, stride, radius);
            // Vertical pass
            BoxBlurVertical(buffer, pixels, bmp.Width, bmp.Height, stride, radius);
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, byteCount);
        bmp.UnlockBits(data);
        return bmp;
    }

    private static void BoxBlurHorizontal(byte[] src, byte[] dst, int w, int h, int stride, int r)
    {
        double invR = 1.0 / (r + r + 1);
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            int rowOff = y * stride;
            
            long sumB = 0, sumG = 0, sumR = 0, sumA = 0;

            for (int i = -r; i <= r; i++)
            {
                int idx = rowOff + Math.Clamp(i, 0, w - 1) * 4;
                sumB += src[idx]; sumG += src[idx + 1]; sumR += src[idx + 2]; sumA += src[idx + 3];
            }

            for (int x = 0; x < w; x++)
            {
                int dstIdx = rowOff + x * 4;
                dst[dstIdx] = (byte)(sumB * invR);
                dst[dstIdx + 1] = (byte)(sumG * invR);
                dst[dstIdx + 2] = (byte)(sumR * invR);
                dst[dstIdx + 3] = (byte)(sumA * invR);

                int addIdx = rowOff + Math.Min(x + r + 1, w - 1) * 4;
                int subIdx = rowOff + Math.Max(x - r, 0) * 4;

                sumB += src[addIdx] - src[subIdx];
                sumG += src[addIdx + 1] - src[subIdx + 1];
                sumR += src[addIdx + 2] - src[subIdx + 2];
                sumA += src[addIdx + 3] - src[subIdx + 3];
            }
        });
    }

    private static void BoxBlurVertical(byte[] src, byte[] dst, int w, int h, int stride, int r)
    {
        double invR = 1.0 / (r + r + 1);
        System.Threading.Tasks.Parallel.For(0, w, x =>
        {
            int colOff = x * 4;
            long sumB = 0, sumG = 0, sumR = 0, sumA = 0;

            for (int i = -r; i <= r; i++)
            {
                int idx = Math.Clamp(i, 0, h - 1) * stride + colOff;
                sumB += src[idx]; sumG += src[idx + 1]; sumR += src[idx + 2]; sumA += src[idx + 3];
            }

            for (int y = 0; y < h; y++)
            {
                int dstIdx = y * stride + colOff;
                dst[dstIdx] = (byte)(sumB * invR);
                dst[dstIdx + 1] = (byte)(sumG * invR);
                dst[dstIdx + 2] = (byte)(sumR * invR);
                dst[dstIdx + 3] = (byte)(sumA * invR);

                int addIdx = Math.Min(y + r + 1, h - 1) * stride + colOff;
                int subIdx = Math.Max(y - r, 0) * stride + colOff;

                sumB += src[addIdx] - src[subIdx];
                sumG += src[addIdx + 1] - src[subIdx + 1];
                sumR += src[addIdx + 2] - src[subIdx + 2];
                sumA += src[addIdx + 3] - src[subIdx + 3];
            }
        });
    }

    /// <summary>
    /// RetroGaming Filter: Simulates retro video game aesthetics by combining adjustable pixel density 
    /// and reduced color depth (quantization).
    /// </summary>
    /// <param name="source">The input full-color high-resolution bitmap.</param>
    /// <param name="intensity">Level of abstraction (1-100). Higher values result in larger pixels and fewer colors.</param>
    /// <returns>A stylized bitmap with a distinct retro hardware feel.</returns>
    private Bitmap ApplyRetroGaming(Bitmap source, int intensity)
    {
        int w = source.Width;
        int h = source.Height;

        // 1. Pixelation factor (1 to 10)
        int pixelSize = Math.Max(1, intensity / 10);
        
        // 2. Color quantization levels (32 levels down to 2 levels per channel)
        int levels = Math.Max(2, 32 - (intensity * 30 / 100)); 
        float step = 255f / (levels - 1);

        Bitmap result;
        
        // Apply Pixelation first using existing logic but scaled
        if (pixelSize > 1)
        {
            int smallW = Math.Max(1, w / pixelSize);
            int smallH = Math.Max(1, h / pixelSize);
            using var small = new Bitmap(smallW, smallH);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.Low;
                g.DrawImage(source, 0, 0, smallW, smallH);
            }
            result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(small, 0, 0, w, h);
            }
        }
        else
        {
            result = (Bitmap)source.Clone();
        }

        // Apply Color Quantization
        var rect = new Rectangle(0, 0, w, h);
        var data = result.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int byteCount = data.Stride * h;
        byte[] pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, byteCount);

        int stride = data.Stride;

        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            int rowOffset = y * stride;
            // Iterate horizontally (each pixel is 4 bytes: B, G, R, A)
            for (int x = 0; x < w; x++)
            {
                int i = rowOffset + x * 4;
                pixels[i]   = (byte)(Math.Round(pixels[i] / step) * step);     // Blue
                pixels[i+1] = (byte)(Math.Round(pixels[i+1] / step) * step);   // Green
                pixels[i+2] = (byte)(Math.Round(pixels[i+2] / step) * step);   // Red
            }
        });

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, byteCount);
        result.UnlockBits(data);

        return result;
    }

    /// <summary>
    /// JpegCompression Filter: Replicates the visual artifacts of low-quality JPEG encoding.
    /// It utilizes block-based processing and chroma subsampling without drawing explicit grid lines.
    /// </summary>
    /// <param name="source">The original high-quality bitmap.</param>
    /// <param name="intensity">The level of compression distortion to simulate.</param>
    /// <returns>A bitmap exhibiting characteristic JPEG blocking and color smearing.</returns>
    private Bitmap ApplyJpegCompression(Bitmap source, int intensity)
    {
        int w = source.Width;
        int h = source.Height;

        // Block size based on intensity (2 to ~27)
        int blockSize = Math.Max(2, 2 + (intensity / 4));
        
        // Chroma subsampling block (color is averaged over a larger area, e.g., 2x2 blocks)
        int chromaSize = blockSize * 2; 

        // Slight color quantization to simulate compression palette limits
        int levels = Math.Max(2, 32 - (intensity * 28 / 100)); 
        float step = 255f / (levels - 1);

        var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, w, h);
        var srcData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int stride = srcData.Stride;
        int byteCount = stride * h;
        byte[] srcPixels = new byte[byteCount];
        byte[] dstPixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, byteCount);

        int yBlocks = (h + blockSize - 1) / blockSize;
        System.Threading.Tasks.Parallel.For(0, yBlocks, blockY =>
        {
            int cy = blockY * blockSize;
            for (int cx = 0; cx < w; cx += blockSize)
            {
                int currentBlockW = Math.Min(blockSize, w - cx);
                int currentBlockH = Math.Min(blockSize, h - cy);

                // 1. Calculate Chroma (Color) over the larger chroma block
                int chromaY = (cy / chromaSize) * chromaSize;
                int chromaX = (cx / chromaSize) * chromaSize;
                int cxW = Math.Min(chromaSize, w - chromaX);
                int cxH = Math.Min(chromaSize, h - chromaY);

                long cR = 0, cG = 0, cB = 0;
                int cCount = 0;
                for (int dy = 0; dy < cxH; dy += 2) // Sample every 2nd pixel for speed
                {
                    for (int dx = 0; dx < cxW; dx += 2)
                    {
                        int MathClamp(int val, int max) => Math.Clamp(val, 0, max - 1);
                        int idx = MathClamp(chromaY + dy, h) * stride + MathClamp(chromaX + dx, w) * 4;
                        cB += srcPixels[idx];
                        cG += srcPixels[idx + 1];
                        cR += srcPixels[idx + 2];
                        cCount++;
                    }
                }
                cCount = Math.Max(1, cCount);
                double avgCr = (double)cR / cCount;
                double avgCg = (double)cG / cCount;
                double avgCb = (double)cB / cCount;

                // 2. Calculate Luminance over the current specific block
                long sL = 0;
                int lCount = 0;
                for (int dy = 0; dy < currentBlockH; dy += 2)
                {
                    for (int dx = 0; dx < currentBlockW; dx += 2)
                    {
                        int MathClamp(int val, int max) => Math.Clamp(val, 0, max - 1);
                        int idx = MathClamp(cy + dy, h) * stride + MathClamp(cx + dx, w) * 4;
                        sL += (long)(srcPixels[idx + 2] * 0.299 + srcPixels[idx + 1] * 0.587 + srcPixels[idx] * 0.114);
                        lCount++;
                    }
                }
                lCount = Math.Max(1, lCount);
                double avgL = (double)sL / lCount;

                // 3. Combine Luminance and Chrominance (Y + C)
                double chromaL = (avgCr * 0.299 + avgCg * 0.587 + avgCb * 0.114);
                double lRatio = chromaL > 0 ? avgL / chromaL : 1.0;

                double finalR = avgCr * lRatio;
                double finalG = avgCg * lRatio;
                double finalB = avgCb * lRatio;

                // 4. Quantize to simulate compression artifacts
                byte blockR = (byte)Math.Clamp(Math.Round(finalR / step) * step, 0, 255);
                byte blockG = (byte)Math.Clamp(Math.Round(finalG / step) * step, 0, 255);
                byte blockB = (byte)Math.Clamp(Math.Round(finalB / step) * step, 0, 255);

                // 5. Apply block color without drawn lines
                for (int dy = 0; dy < currentBlockH; dy++)
                {
                    for (int dx = 0; dx < currentBlockW; dx++)
                    {
                        int idx = (cy + dy) * stride + (cx + dx) * 4;
                        
                        dstPixels[idx] = blockB;
                        dstPixels[idx + 1] = blockG;
                        dstPixels[idx + 2] = blockR;
                        dstPixels[idx + 3] = 255; // 100% Opaque
                    }
                }
            }
        });

        System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, byteCount);
        source.UnlockBits(srcData);
        result.UnlockBits(dstData);

        return result;
    }
}
