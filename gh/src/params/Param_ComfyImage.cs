using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Net;
using System.IO;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ComfyGH.Types;
using ComfyGH.Attributes;
using System.Threading.Tasks;

namespace ComfyGH.Params
{
    public class Param_ComfyImage : GH_Param<GH_ComfyImage>
    {
        // ビットマップ画像を格納するプライベート変数。
        private Bitmap _image;

        // 画像のキーを格納するプライベート変数。
        private int _imageKey;

        public Param_ComfyImage() : base(new GH_InstanceDescription("ComfyImage", "CI", "ComfyImage", "ComfyGH", "Params"))
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new ImagePreviewAttributes(this);
        }

        public override void ExpireSolution(bool recompute)
        {
            _image = null;
            _imageKey++;
            base.ExpireSolution(recompute);
        }

        // キャッシュされたプレビュー画像を取得するメソッド。
        public ImageUriImageLoading GetCachedPreview(out Bitmap image)
        {
            image = _image;

            if (image != null)
            {
                return ImageUriImageLoading.FinishedLoading;
            }

            if (m_data.IsEmpty)
            {
                return ImageUriImageLoading.None;
            }

            // Thread thread = new Thread((ThreadStart)delegate
            // {
            //     LoadPreviewImage(_imageKey);
            // });
            // thread.IsBackground = true;
            // thread.Priority = ThreadPriority.BelowNormal;
            // thread.Start();
            _ = LoadPreviewImage(_imageKey);

            return ImageUriImageLoading.BusyLoading;
        }


        // プレビュー画像を読み込むためのメソッド。
        private async Task LoadPreviewImage(int key)
        {
            if (key != _imageKey)
            {
                return;
            }

            List<ComfyImage> list = new List<ComfyImage>();

            foreach (IGH_Goo item in m_data.AllData(skipNulls: true))
            {
                if (item is GH_ComfyImage comfyImage)
                {
                    list.Add(new ComfyImage(comfyImage.Value));
                }
            }

            if (list.Count == 0) return;


            Bitmap bitmap = list[0].bitmap;


            if (key == _imageKey)
            {
                _image = bitmap;
            }
        }


        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override Guid ComponentGuid => new Guid("86A392FF-3688-414B-A50D-453245A2C418");
    }
}

