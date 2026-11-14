using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace PNG2CUR
{
    public partial class Form1 : Form
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;
        private readonly List<Image> controlImages = new();

        private PictureBox previewBox;
        private NumericUpDown hotXNum, hotYNum;
        private Button selectBtn, convertBtn, batchConvertBtn, previousImageBtn, nextImageBtn;
        private Label lblHot, lblHotY;
        private string pngPath = "";
        private string[] previewPaths = Array.Empty<string>();
        private int previewIndex;
        private const int MaxSize = 256; // Windows 光标最大尺寸

        public Form1()
        {
            Text = "[PNG to CUR Converter]";
            BackgroundImage = LoadControlImage("bg.png");
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(350, 300);
            Padding = new Padding(12);
            Disposed += (_, _) =>
            {
                foreach (Image image in controlImages)
                {
                    image.Dispose();
                }
            };

            selectBtn = new Button
            {
                Text = "Select PNG image",
                Location = new Point(12, 12),
                Width = 100,
                BackgroundImage = LoadControlImage("selectBtn.png"),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                ForeColor = Color.Black
            };
            selectBtn.FlatAppearance.BorderSize = 0;
            selectBtn.Click += SelectPng;

            previewBox = new PictureBox { Location = new Point(12, selectBtn.Bottom + 12), Size = new Size(160, 160),
                                        BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom,
                                        BackgroundImage = LoadControlImage("previewBox.png"), BackgroundImageLayout = ImageLayout.Stretch };

            previousImageBtn = new Button
            {
                Text = "↑",
                Location = new Point(180, 82),
                Size = new Size(32, 32),
                BackColor = Color.FromArgb(148, 148, 148),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            previousImageBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            previousImageBtn.Click += PreviousImageClick;

            nextImageBtn = new Button
            {
                Text = "↓",
                Location = new Point(180, 122),
                Size = new Size(32, 32),
                BackColor = Color.FromArgb(148, 148, 148),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            nextImageBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            nextImageBtn.Click += NextImageClick;

            Label lblHotTitle = new Label { Text = "Hotspot:", Location = new Point(230, 45), AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            lblHot = new Label { Text = "X:", Location = new Point(230, 70), AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent };
            hotXNum = new NumericUpDown { Location = new Point(258, 70), Width = 60, Minimum = 0, Maximum = 255,
                                          BackColor = Color.FromArgb(148, 148, 148), ForeColor = Color.Black };
            lblHotY = new Label { Text = "Y:", Location = new Point(230, 95), AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent };
            hotYNum = new NumericUpDown { Location = new Point(258, 95), Width = 60, Minimum = 0, Maximum = 255,
                                          BackColor = Color.FromArgb(148, 148, 148), ForeColor = Color.Black };

            convertBtn = new BlackTextButton
            {
                Text = "Convert to CUR file",
                Location = new Point(210, 125),
                AutoSize = false,
                Size = new Size(125, 30),
                BackgroundImage = LoadControlImage("ConvertToCURfile.png"),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                ForeColor = Color.Black
            };
            convertBtn.FlatAppearance.BorderSize = 0;
            convertBtn.Click += ConvertClick;
            convertBtn.Enabled = false;

            batchConvertBtn = new Button
            {
                Text = "Multi convert",
                Location = new Point(selectBtn.Right + 12, 12),
                Width = 110,
                BackgroundImage = LoadControlImage("multiConvert.png"),
                BackgroundImageLayout = ImageLayout.Stretch,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                ForeColor = Color.Black
            };
            batchConvertBtn.FlatAppearance.BorderSize = 0;
            batchConvertBtn.Click += SelectImagesForPreview;

            convertBtn.Location = new Point(previewBox.Left, previewBox.Bottom + 12);

            Controls.AddRange(new Control[] { selectBtn, batchConvertBtn, previewBox, previousImageBtn, nextImageBtn,
                lblHotTitle, lblHot, hotXNum, lblHotY, hotYNum, convertBtn });
            UpdateNavigationButtons();
        }

        private sealed class BlackTextButton : Button
        {
            public BlackTextButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (BackgroundImage != null)
                {
                    e.Graphics.DrawImage(BackgroundImage, ClientRectangle);
                }
                else
                {
                    e.Graphics.Clear(BackColor);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    ClientRectangle,
                    Color.Black,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                if (Focused && ShowFocusCues)
                {
                    ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
                }
            }
        }

        private Image LoadControlImage(string fileName)
        {
            string imagePath = Path.Combine(AppContext.BaseDirectory, "imgs", fileName);
            using Image source = Image.FromFile(imagePath);
            Image image = new Bitmap(source);
            controlImages.Add(image);
            return image;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            int black = 0x000000;
            int white = 0x00FFFFFF;
            int captionResult = DwmSetWindowAttribute(Handle, DwmwaCaptionColor, ref black, sizeof(int));
            int textResult = DwmSetWindowAttribute(Handle, DwmwaTextColor, ref white, sizeof(int));
            if (captionResult != 0 || textResult != 0)
            {
                int enabled = 1;
                int result = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
                }
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        private void SelectPng(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "PNG Image|*.png" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            previewPaths = new[] { ofd.FileName };
            previewIndex = 0;
            ShowSelectedImage();
        }

        private void ConvertClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(pngPath)) return;

            int imageWidth = previewBox.Image?.Width ?? 0;
            int imageHeight = previewBox.Image?.Height ?? 0;
            int hotX = (int)hotXNum.Value;
            int hotY = (int)hotYNum.Value;

            if (imageWidth > MaxSize || imageHeight > MaxSize)
            {
                MessageBox.Show($"Image size {imageWidth}x{imageHeight} exceeded the maximum allowed size {MaxSize}x{MaxSize}! Please try again after resizing the image.", "[Size exceeded the limit]");
                return;
            }

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

        private void SelectImagesForPreview(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "PNG Image|*.png",
                Multiselect = true,
                Title = "Select PNG images to preview"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            previewPaths = ofd.FileNames;
            previewIndex = 0;
            ShowSelectedImage();
        }

        private void PreviousImageClick(object? sender, EventArgs e)
        {
            ChangePreview(-1);
        }

        private void NextImageClick(object? sender, EventArgs e)
        {
            ChangePreview(1);
        }

        private void ChangePreview(int direction)
        {
            if (previewPaths.Length < 2) return;

            previewIndex = (previewIndex + direction + previewPaths.Length) % previewPaths.Length;
            ShowSelectedImage();
        }

        private void ShowSelectedImage()
        {
            UpdateNavigationButtons();
            if (previewPaths.Length == 0)
            {
                pngPath = "";
                previewBox.Image?.Dispose();
                previewBox.Image = null;
                convertBtn.Enabled = false;
                return;
            }

            pngPath = previewPaths[previewIndex];
            try
            {
                using var image = Image.FromFile(pngPath);
                Image previewImage = new Bitmap(image);
                Image? previousImage = previewBox.Image;
                previewBox.Image = previewImage;
                previousImage?.Dispose();

                hotXNum.Value = hotXNum.Minimum;
                hotYNum.Value = hotYNum.Minimum;
                hotXNum.Maximum = Math.Min(MaxSize - 1, image.Width - 1);
                hotYNum.Maximum = Math.Min(MaxSize - 1, image.Height - 1);
                hotXNum.Value = Math.Min(hotXNum.Maximum, image.Width / 2);
                hotYNum.Value = Math.Min(hotYNum.Maximum, image.Height / 2);

                bool withinLimit = image.Width <= MaxSize && image.Height <= MaxSize;
                convertBtn.Enabled = withinLimit;
                if (!withinLimit)
                {
                    MessageBox.Show($"Image size {image.Width}x{image.Height} exceeded the maximum allowed size {MaxSize}x{MaxSize}! Please try again after resizing the image.", "[Size exceeded the limit]");
                }
            }
            catch (Exception ex)
            {
                pngPath = "";
                previewBox.Image?.Dispose();
                previewBox.Image = null;
                convertBtn.Enabled = false;
                MessageBox.Show("Error loading image: " + ex.Message, "[Error]");
            }
        }

        private void UpdateNavigationButtons()
        {
            bool canNavigate = previewPaths.Length > 1;
            previousImageBtn.Enabled = canNavigate;
            nextImageBtn.Enabled = canNavigate;
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