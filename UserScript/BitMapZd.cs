using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UserScript
{
    public static class BitMapZd
    {
        // <summary>
        /// 会产生graphics异常的PixelFormat
        /// </summary>
        private static PixelFormat[] indexedPixelFormats = { PixelFormat.Undefined, PixelFormat.DontCare,
                                                             PixelFormat.Format16bppArgb1555, PixelFormat.Format1bppIndexed, PixelFormat.Format4bppIndexed,
                                                             PixelFormat.Format8bppIndexed};


        /// <summary>
        /// 判断图片的PixelFormat 是否在 引发异常的 PixelFormat 之中
        /// </summary>
        /// <param name="imgPixelFormat">原图片的PixelFormat</param>
        /// <returns></returns>
        private static bool IsPixelFormatIndexed(PixelFormat imgPixelFormat)
        {
            foreach (PixelFormat pf in indexedPixelFormats)
            {
                if (pf.Equals(imgPixelFormat)) return true;
            }

            return false;
        }
        public static  Bitmap DrawCircle(Bitmap image,float x, float y, float diameter, bool hollow = false,float linewidth=3, Color? color=null )
        {
            Bitmap bmp = null;
            float r = diameter / 2;
            if (IsPixelFormatIndexed(image.PixelFormat))
            {
                bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(image, 0, 0);
                    Color _color=(Color)((color==null)?Color.Green:color);
                    if (!hollow)
                    {
                        g.FillEllipse(new SolidBrush(_color), x - r, y - r, diameter, diameter);

                    }
                    else
                        g.DrawEllipse(new Pen(_color, linewidth), x - r, y - r, diameter, diameter);
                }
            }
            else
            {
                using (Graphics g = Graphics.FromImage(image))
                {
                    Color _color = (Color)((color == null) ? Color.Green : color);
                    if (!hollow)
                    {
                        g.FillEllipse(new SolidBrush(_color), x - r, y - r, diameter, diameter);
                    }
                    else
                        g.DrawEllipse(new Pen(_color, linewidth), x - r, y - r, diameter, diameter);
                }
                bmp = image;
            }
            return bmp;
        }
        public static Bitmap DrawLine(Bitmap image,float rowstart,float columnstart,float rowend,float columnend, float linewidth = 3, Color? color = null)
        {
            Bitmap bmp = null;
            if (IsPixelFormatIndexed(image.PixelFormat))
            {
                bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(image, 0, 0);
                    Color _color = (Color)((color == null) ? Color.Green : color);
                    g.DrawLine(new Pen(_color, linewidth), rowstart, columnstart, rowend, columnend);
                }
            }
            else
            {
                using (Graphics g = Graphics.FromImage(image))
                {
                    Color _color = (Color)((color == null) ? Color.Green : color);
                    g.DrawLine(new Pen(_color, linewidth), rowstart, columnstart, rowend, columnend);
                }
                bmp = image;
            }
            return bmp;
        }
        public static Bitmap DrawCross(Bitmap image, float x, float y,double  angle, float size=30, float linewidth = 3, Color? color = null)
        {
            Bitmap bmp = null;
           
            if (IsPixelFormatIndexed(image.PixelFormat))
            {
                bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.DrawImage(image, 0, 0);
                    Color _color = (Color)((color == null) ? Color.Green : color);
                  //  float k =(float) Math.Sin((float)45/180*Math.PI);
                    float xoffset1 = size * (float)Math.Sin(angle/180*Math.PI);
                    float yoffset1 = size * (float)Math.Cos(angle / 180 * Math.PI);
                    float xoffset2 = size * (float)Math.Sin((angle+(float)90) / 180 * Math.PI);
                    float yoffset2 = size * (float)Math.Cos((angle+(float)90) / 180 * Math.PI);

                    g.DrawLine(new Pen(_color, linewidth), x - xoffset1, y- yoffset1, x + xoffset1, y+ yoffset1);
                    g.DrawLine(new Pen(_color, linewidth), x- xoffset2, y - yoffset2, x+ xoffset2, y + yoffset2);
                }
            }
            else
            {
                using (Graphics g = Graphics.FromImage(image))
                {
                    Color _color = (Color)((color == null) ? Color.Green : color);


                    float xoffset1 = size * (float)Math.Sin(angle / 180 * Math.PI);
                    float yoffset1 = size * (float)Math.Cos(angle / 180 * Math.PI);
                    float xoffset2 = size * (float)Math.Sin((angle + (float)90) / 180 * Math.PI);
                    float yoffset2 = size * (float)Math.Cos((angle + (float)90) / 180 * Math.PI);

                    g.DrawLine(new Pen(_color, linewidth), x - xoffset1, y - yoffset1, x + xoffset1, y + yoffset1);
                    g.DrawLine(new Pen(_color, linewidth), x - xoffset2, y - yoffset2, x + xoffset2, y + yoffset2);
                }
                bmp = image;
            }


            return bmp;
        }
        

    }
}
