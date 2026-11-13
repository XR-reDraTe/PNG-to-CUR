using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PNG2CUR
{
    public partial class Form1 : Form
    {
        private PictureBox previewBox;
        private NumericUpDown hotXNum, hotYNum;
        private Button selectBtn, convertBtn, batchConvertBtn;
        private Label lblHot, lblHotY;
        private string pngPath = "";
        private const int MaxSize = 256; // Windows 光标最大尺寸

        public Form1()
        {
            Text = "[PNG to CUR Converter]";
            Size = new Size(320, 300);
            Padding = new Padding(12);

            selectBtn = new Button { Text = "Select PNG image", Location = new Point(12, 12), Width = 100 };
            selectBtn.Click += SelectPng;

            previewBox = new PictureBox { Location = new Point(12, selectBtn.Bottom + 12), Size = new Size(160, 160),
                                        BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };

            Label lblHotTitle = new Label { Text = "Hotspot:", Location = new Point(182, 45), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            lblHot = new Label { Text = "X:", Location = new Point(182, 70), AutoSize = true };
            hotXNum = new NumericUpDown { Location = new Point(210, 70), Width = 60, Minimum = 0, Maximum = 255 };
            lblHotY = new Label { Text = "Y:", Location = new Point(182, 95), AutoSize = true };
            hotYNum = new NumericUpDown { Location = new Point(210, 95), Width = 60, Minimum = 0, Maximum = 255 };

            convertBtn = new Button { Text = "Convert to CUR file", Location = new Point(210, 125), AutoSize = false, Size = new Size(125, 30) };
            convertBtn.Click += ConvertClick;
            convertBtn.Enabled = false;

            batchConvertBtn = new Button { Text = "Multi convert", Location = new Point(selectBtn.Right + 12, 12), Width = 110 };
            batchConvertBtn.Click += BatchConvertClick;

            convertBtn.Location = new Point(previewBox.Left, previewBox.Bottom + 12);

            Controls.AddRange(new Control[] { selectBtn, batchConvertBtn, previewBox, lblHotTitle, lblHot, hotXNum, lblHotY, hotYNum, convertBtn });
        }

        private void SelectPng(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "PNG Image|*.png" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            pngPath = ofd.FileName;
            try
            {
                using var fs = new FileStream(ofd.FileName, FileMode.Open);
                using var img = Image.FromStream(fs);
                if (img.Width > MaxSize || img.Height > MaxSize)
                {
                    MessageBox.Show($"Image size {img.Width}x{img.Height} exceeded the maximum allowed size {MaxSize}x{MaxSize}! Please try again after resizing the image.", "[Size exceeded the limit]");
                    pngPath = "";
                    previewBox.Image = null;
                    convertBtn.Enabled = false;
                    return;
                }

                previewBox.Image = new Bitmap(img);
                hotXNum.Maximum = Math.Min(MaxSize - 1, img.Width - 1);
                hotYNum.Maximum = Math.Min(MaxSize - 1, img.Height - 1);
                hotXNum.Value = img.Width / 2;
                hotYNum.Value = img.Height / 2;
                convertBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                pngPath = "";
                previewBox.Image = null;
                convertBtn.Enabled = false;
                MessageBox.Show("Error loading image: " + ex.Message, "[Error]");
                return;
            }
        }

        private void ConvertClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(pngPath)) return;

            int imageWidth = previewBox.Image?.Width ?? 0;
            int imageHeight = previewBox.Image?.Height ?? 0;
            int hotX = (int)hotXNum.Value;
            int hotY = (int)hotYNum.Value;

            if (hotX >= imageWidth || hotY >= imageHeight)
            {
                MessageBox.Show("Hotspot coordinates are outside the selected image bounds.", "[Invalid Hotspot]");
                return;
            }

            using var sfd = new SaveFileDialog { Filter = "CUR File|*.cur", FileName = Path.GetFileNameWithoutExtension(pngPath) + ".cur" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                PngToCurConverter.Convert(pngPath, sfd.FileName, hotX, hotY);
                MessageBox.Show("Successfully converted!", "[Success]");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to convert: " + ex.Message, "[Error]");
            }
        }

        private void BatchConvertClick(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "PNG Image|*.png",
                Multiselect = true,
                Title = "Select PNG images to convert"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            using var fbd = new FolderBrowserDialog { Description = "Select the output folder for CUR files" };
            if (fbd.ShowDialog(this) != DialogResult.OK) return;

            int hotX = (int)hotXNum.Value;
            int hotY = (int)hotYNum.Value;
            int convertedCount = 0;
            var failures = new System.Collections.Generic.List<string>();

            foreach (string sourcePath in ofd.FileNames)
            {
                string fileName = Path.GetFileName(sourcePath);
                try
                {
                    int imageWidth;
                    int imageHeight;
                    using (var image = Image.FromFile(sourcePath))
                    {
                        imageWidth = image.Width;
                        imageHeight = image.Height;
                    }

                    if (imageWidth > MaxSize || imageHeight > MaxSize)
                    {
                        failures.Add($"{fileName}: image exceeds the maximum size of {MaxSize}x{MaxSize}.");
                        continue;
                    }

                    if (hotX >= imageWidth || hotY >= imageHeight)
                    {
                        failures.Add($"{fileName}: hotspot coordinates are outside the image bounds.");
                        continue;
                    }

                    string destinationPath = Path.Combine(fbd.SelectedPath, Path.GetFileNameWithoutExtension(sourcePath) + ".cur");
                    if (File.Exists(destinationPath))
                    {
                        failures.Add($"{fileName}: output file already exists ({Path.GetFileName(destinationPath)}).");
                        continue;
                    }

                    PngToCurConverter.Convert(sourcePath, destinationPath, hotX, hotY);
                    convertedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{fileName}: {ex.Message}");
                }
            }

            if (failures.Count == 0)
            {
                MessageBox.Show($"Successfully converted {convertedCount} file(s).", "[Success]");
                return;
            }

            string message = $"Converted: {convertedCount}\nFailed: {failures.Count}\n\n" + string.Join(Environment.NewLine, failures);
            MessageBox.Show(message, "[Batch conversion results]");
        }
    }


    static class PngToCurConverter
    {
        public static void Convert(string pngPath, string curPath, int hotX, int hotY)
        {
            using var bmp = new Bitmap(pngPath);
            int w = bmp.Width, h = bmp.Height;

            // 锁定像素
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;
            byte[] pixels = new byte[stride * h];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            // DIB header (BITMAPINFOHEADER)
            int dibSize = 40;
            int xorStride = ((w * 32 + 31) / 32) * 4;      // XOR行对齐到4字节
            int andStride = ((w + 31) / 32) * 4;            // AND行对齐到4字节
            int xorSize = xorStride * h;
            int andSize = andStride * h;

            byte[] dib = new byte[dibSize + xorSize + andSize];

            // BITMAPINFOHEADER
            BitConverter.GetBytes(dibSize).CopyTo(dib, 0);          // biSize
            BitConverter.GetBytes(w).CopyTo(dib, 4);                 // biWidth
            BitConverter.GetBytes(h * 2).CopyTo(dib, 8);             // biHeight (双倍高度表示图标/光标)
            BitConverter.GetBytes((short)1).CopyTo(dib, 12);         // biPlanes
            BitConverter.GetBytes((short)32).CopyTo(dib, 14);        // biBitCount
            BitConverter.GetBytes(0).CopyTo(dib, 16);                // biCompression (BI_RGB)

            // XOR数据 (BGRA -> 写入DIB)
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * stride;
                int dstRow = (h - 1 - y) * xorStride; // BMP存储倒序
                for (int x = 0; x < w; x++)
                {
                    int si = srcRow + x * 4;
                    int di = dibSize + dstRow + x * 4;
                    // BGRA -> BGRA (无需交换)
                    dib[di] = pixels[si];       // B
                    dib[di+1] = pixels[si+1];   // G
                    dib[di+2] = pixels[si+2];   // R
                    dib[di+3] = pixels[si+3];   // A
                }
            }

            // AND掩码 (透明度: alpha<128 视为透明，对应掩码位=1)
            int andOffset = dibSize + xorSize;
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * stride;
                int dstRow = (h - 1 - y) * andStride;
                for (int x = 0; x < w; x++)
                {
                    byte alpha = pixels[srcRow + x * 4 + 3];
                    if (alpha < 128)
                    {
                        int byteIdx = andOffset + dstRow + (x / 8);
                        int bitIdx = 7 - (x % 8);
                        dib[byteIdx] |= (byte)(1 << bitIdx);
                    }
                }
            }

            // 构造CUR文件
            using var fs = new FileStream(curPath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // ICO/CUR header
            bw.Write((short)0);     // reserved
            bw.Write((short)2);     // type=2 光标
            bw.Write((short)1);     // count=1个图像

            // 目录项
            int imageSize = dibSize + xorSize + andSize;
            bw.Write((byte)(w == 256 ? 0 : w));     // width (0表示256)
            bw.Write((byte)(h == 256 ? 0 : h));     // height
            bw.Write((byte)0);                      // color palette count
            bw.Write((byte)0);                      // reserved
            bw.Write((short)hotX);                  // hotspot X
            bw.Write((short)hotY);                  // hotspot Y
            bw.Write(imageSize);                    // bytes in resource
            bw.Write(22);                           // offset from start (header 6 + dir entry 16 = 22)

            // 写入DIB数据
            bw.Write(dib);
        }
    }
}

/// ■       ■   ■ ■ ■ ■             ■ ■ ■ ■     ■ ■ ■ ■ ■   ■ ■ ■ ■     ■ ■ ■ ■         ■       ■ ■ ■ ■ ■   ■ ■ ■ ■ ■
///   ■   ■     ■       ■           ■       ■   ■           ■       ■   ■       ■     ■   ■         ■       ■
///     ■       ■ ■ ■ ■     ■ ■ ■   ■ ■ ■ ■     ■ ■ ■ ■     ■       ■   ■ ■ ■ ■     ■ ■ ■ ■ ■       ■       ■ ■ ■ ■
///   ■   ■     ■       ■           ■       ■   ■           ■       ■   ■       ■   ■       ■       ■       ■
/// ■       ■   ■       ■           ■       ■   ■ ■ ■ ■ ■   ■ ■ ■ ■     ■       ■   ■       ■       ■       ■ ■ ■ ■ ■