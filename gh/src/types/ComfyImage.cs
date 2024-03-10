using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ComfyGH.Attributes;

namespace ComfyGH
{
    public class ComfyImage
    {   
        public Bitmap bitmap;

        public ComfyImage(Bitmap bitmap)
        {
            if(bitmap == null)
                throw new Exception("Bitmap is null");
            this.bitmap = bitmap;
        }

        public ComfyImage(string path)
        {
            if(!System.IO.File.Exists(path))
                throw new Exception("File does not exist");
            this.bitmap = new Bitmap(path);
        }

        public ComfyImage(ComfyImage image)
        {
            this.bitmap = new Bitmap(image.bitmap);
        }

        public string ToBase64String()
        {
            lock (ImagePreviewAttributes.bitmapLock)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    this.bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bytes = stream.ToArray();
                    string base64Image = Convert.ToBase64String(bytes);
                    return base64Image;
                }
            }
        }
        
    }
}