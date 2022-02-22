using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyTextReaderHost
{

    internal class TextLibGen
    {
        public Font Font { get; private set; }
        public Size SingleCharSize { get; private set; }
        private Bitmap bitmap;
        private Graphics graphics;

        public TextLibGen(Font fontdata)
        {
            Font = fontdata;
            bitmap = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(bitmap);
            SingleCharSize = g.MeasureString("字", fontdata).ToSize();
            bitmap = new Bitmap(SingleCharSize.Width, SingleCharSize.Height);
            graphics = Graphics.FromImage(bitmap);
        }

        public byte[] GetSingleDrawing(string character)
        {
            List<byte> bytes = new List<byte>();
            graphics.FillRectangle(Brushes.White, new Rectangle() { X = 0, Y = 0, Height = SingleCharSize.Height, Width = SingleCharSize.Width });
            graphics.DrawString(character, Font, Brushes.Black, new PointF());
            byte bytetmp = 0;
            byte bytecnt = 0;
            int cntt = 0;
            for (int x = 0; x < SingleCharSize.Width; x++)
            {
                for (int y = 0; y < SingleCharSize.Height; y++)
                {
                    cntt++;
                    byte item = (byte)(bitmap.GetPixel(x, y).GetBrightness() > 0.5f ? 0 : 1);
                    if (bytecnt < 7)
                    {
                        bytecnt++;
                        bytetmp = (byte)((bytetmp << 1) + item);
                    }
                    else
                    {
                        bytetmp = (byte)((bytetmp << 1) + item);
                        bytes.Add(bytetmp);
                        bytetmp = 0;
                        bytecnt = 0;
                    }
                }
            }
            return bytes.ToArray();
        }

        public byte[] GetSingleDrawing(char character)
        {
            return GetSingleDrawing(character.ToString());
        }

        public byte[] GetSingleDrawing(byte[] character)
        {
            return GetSingleDrawing(character.ToString());
        }

        public byte[] GetSingleDrawing(int unicode)
        {
            return GetSingleDrawing(UnicodeSingleCharString(unicode));
        }

        public static string UnicodeSingleCharString(int code)
        {
            return Encoding.Unicode.GetString(BitConverter.GetBytes(code))[..1];
        }

        public static int SingleUnicode(string str)
        {
            var bytes = Encoding.Unicode.GetBytes(str);
            return BitConverter.ToInt32(bytes);
        }

        public Dictionary<int, byte[]> GetAllTextDrawing(
            bool ascii = true, bool basic_chn = true, bool extend_chn = true, bool extend_ext_chn = true,
            bool extend_kxbs = true, bool extend_bs = true, bool strokes = true, bool tone_marks = true
            )
        {
            Dictionary<int, byte[]> bytes = new Dictionary<int, byte[]>();
            if (ascii)//ascii部分
                for (int x = 32; x <= 126; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            if (basic_chn)//基本汉字
                for (int x = '\u4E00'; x <= '\u9FFF'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            //汉字扩展
            if (extend_chn)
            {
                for (int x = '\u3400'; x <= '\u4DBF'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
                bytes.Add(0x3007, GetSingleDrawing(0x3007));
            }
            //汉字增强扩展
            if (extend_ext_chn)
            {
                for (int x = 0x20000; x <= 0x2A6DF; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
                for (int x = 0x2A700; x <= 0x2B738; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
                for (int x = 0x2B740; x <= 0x2B81D; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
                for (int x = 0x2B820; x <= 0x2CEA1; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
                for (int x = 0x2CEB0; x <= 0x2EBE0; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
                for (int x = 0x30000; x <= 0x3134A; x++)
                {
                    bytes.Add(x, GetSingleDrawing(x));
                }
            }
            //康熙部首
            if (extend_kxbs)
                for (int x = '\u2F00'; x <= '\u2FD5'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            if (extend_bs)//部首
                for (int x = '\u2E80'; x <= '\u2EF3'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            if (strokes)//笔画
                for (int x = '\u31C0'; x <= '\u31E3'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            //注音符号
            if (tone_marks)
            {
                for (int x = '\u3105'; x <= '\u312F'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
                for (int x = '\u31A0'; x <= '\u31BA'; x++)
                {
                    char c = (char)x;
                    bytes.Add(c, GetSingleDrawing(c));
                }
            }
            return bytes;
        }

        public static void SaveTextDrawingToFile(string filename, Dictionary<int, byte[]> data, Size singlechar)
        {
            int dataoffset = 8 + data.Count * 8;
            int singlesize = data.FirstOrDefault().Value.Length;
            BinaryWriter sr = new BinaryWriter(File.OpenWrite(filename));
            int cnt = 0;

            //生成字符组信息
            //8 byte:
            //Width Height 00 00   00 00 00 00
            //00区域保留待用
            {
                byte[] buffer = new byte[8];
                buffer[0] = (byte)singlechar.Width;
                buffer[1] = (byte)singlechar.Height;
            }

            //生成引导表
            //单条目 8byte
            //4byte_utf8字符编码  +  4byte字符地址码
            {
                foreach (KeyValuePair<int, byte[]> kvp in data)
                {
                    int dataaddr = dataoffset + singlesize * cnt;

                    //4byte utf8
                    byte[] charcode = new byte[4] { 0, 0, 0, 0 };
                    {
                        byte[] bfcode = Encoding.UTF8.GetBytes(UnicodeSingleCharString(kvp.Key));
                        int ii = 0;
                        int bbfoffset = 4 - bfcode.Length;
                        foreach (byte bt in bfcode)
                        {
                            charcode[bbfoffset + ii] = bt;
                            ii++;
                        }
                    }

                    //4byte address
                    byte[] addr = BitConverter.GetBytes(dataaddr);

                    List<byte> buffer = new List<byte>(8);
                    buffer.AddRange(charcode);
                    buffer.AddRange(addr);

                    sr.Write(buffer.ToArray());
                    cnt++;
                }

            }

            if (sr.BaseStream.Position != dataoffset)
            {
                throw new FormatException("Data address offset mismatch.");
            }

            //字库主体数据
            foreach (var d in data.Values)
            {
                sr.Write(d);
            }
            sr.Flush();
            sr.Close();
        }
    }
}
