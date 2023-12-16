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

        // 属性を作成するメソッド。カスタム属性クラスをこのパラメータに割り当てます。
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
            // 出力パラメータとして_imageを設定します。
            // 呼び出し元はこのメソッドを通じて_imageの現在の状態を取得できます。
            image = _image;
            
            // _imageがnullでない場合、つまりすでに画像がキャッシュされている場合、
            // ImageUriImageLoading.FinishedLoadingを返します。
            // これは画像の読み込みが完了していることを意味します。
            if (image != null)
            {
                return ImageUriImageLoading.FinishedLoading;
            }

            // m_dataが空の場合（データが何もない場合）、
            // ImageUriImageLoading.Noneを返します。
            // これは画像が存在しないことを意味します。
            if (m_data.IsEmpty)
            {
                return ImageUriImageLoading.None;
            }

            // 新しいスレッドを作成して画像の読み込み処理を開始します。
            // このスレッドはLoadPreviewImageメソッドをバックグラウンドで実行します。
            // スレッドの優先度はThreadPriority.BelowNormalに設定されており、
            // これにより他のより重要なスレッドの実行を妨げないようにしています。
            Thread thread = new Thread((ThreadStart)delegate
            {
                LoadPreviewImage(_imageKey);
            });
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();

            // 画像がまだ読み込まれていない（読み込み中）であることを示すために、
            // ImageUriImageLoading.BusyLoadingを返します。
            return ImageUriImageLoading.BusyLoading;
        }


        // プレビュー画像を読み込むためのメソッド。
        private void LoadPreviewImage(int key)
        {
            // 現在の_imageKeyと引数で渡されたkeyが異なる場合、
            // このメソッドは何もせずに終了します。
            if (key != _imageKey)
            {
                return;
            }

            // URIのリストを作成します。
            //List<string> list = new List<string>();
            List<GH_ComfyImage> list = new List<GH_ComfyImage>();
            // m_dataはGH_Paramクラスから継承されたプロパティで、
            // パラメータが持つデータのコレクションです。
            // このループでは、有効なデータ（nullでないデータ）をすべて処理します。
            foreach (IGH_Goo item in m_data.AllData(skipNulls: true))
            {
                // データ項目が文字列型（GH_String）で、かつ空でない場合、
                // その文字列をリストに追加します。
                // if (item is GH_String gH_String && !string.IsNullOrEmpty(gH_String.Value))
                // {
                //     list.Add(gH_String.Value);
                // }
                if (item is GH_ComfyImage)
                {
                    list.Add(item as GH_ComfyImage);
                }
            }

            // リストに何も追加されなかった場合、このメソッドは何もせずに終了します。
            if (list.Count == 0)
            {
                return;
            }

            // 新しいビットマップ画像を作成します。
            // ここでは、400x400ピクセル、32ビットARGBフォーマットの画像を作成しています。
            Bitmap bitmap = new Bitmap(400, 400, PixelFormat.Format32bppPArgb);

            // 一つの画像あたりの横幅を計算します。
            double num = (double)bitmap.Width / (double)list.Count;

            // 画像の描画にGraphicsクラスを使用します。
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                // 透明色で画像をクリアします。
                graphics.Clear(Color.Transparent);

                // 画像の描画品質を設定します。
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // リスト内の各URIから画像を読み込み、ビットマップに描画します。
                for (int i = 0; i < list.Count; i++)
                {
                    // URIから画像を読み込みます。
                    //Bitmap bitmap2 = TryLoadImage(list[i]);
                    ComfyImage comfyImage = list[i].Value;
                    Bitmap bitmap2 = comfyImage.bitmap;
                    if (bitmap2 != null)
                    {
                        // 各画像の描画位置を計算し、クリップ領域を設定して描画します。
                        int x = (int)(num * (double)i);
                        graphics.SetClip(new Rectangle(x, 0, (int)num, bitmap.Height));
                        graphics.DrawImage(bitmap2, 0, 0, bitmap.Width, bitmap.Height);
                        // クリップ領域をリセットします。
                        graphics.ResetClip();
                    }
                }
            }

            // キーが現在の_imageKeyと同じであれば、
            // 生成したビットマップを_image変数に格納します。
            // これにより、後でGetCachedPreviewメソッドからアクセスできるようになります。
            if (key == _imageKey)
            {
                _image = bitmap;
            }
        }


        // URIから画像を読み込む試みをするメソッド。
        private static Bitmap TryLoadImage(string uri)
        {
            // 最初のtryブロックでは、URIを使ってBitmapオブジェクトを直接作成しようとします。
            // このURIは、ファイルパスまたはローカルファイルシステム上の画像へのパスである可能性があります。
            try
            {
                return new Bitmap(uri);
            }
            catch
            {
                // URIから直接画像を読み込むことに失敗した場合、
                // ここでキャッチされ、次の手段に移ります。
            }

            // 第二のtryブロックでは、WebClientを使用して画像をWebからダウンロードしようとします。
            // この場合、URIはWeb上のリソース（例えばHTTP URL）を指していると考えられます。
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    // URIを使ってWebからストリームを開き、
                    // そのストリームから直接Bitmapを作成します。
                    using (Stream stream = webClient.OpenRead(uri))
                    {
                        // ストリームがnullでない場合、それを使ってBitmapを作成します。
                        if (stream == null)
                        {
                            return null;
                        }
                        return new Bitmap(stream);
                    }
                }
            }
            catch
            {
                // Webからの画像の読み込みにも失敗した場合、
                // ここでキャッチされます。
            }

            // どちらの方法でも画像を読み込むことができなかった場合は、nullを返します。
            // これは、URIから有効な画像を取得できなかったことを意味します。
            return null;
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override Guid ComponentGuid => new Guid("86A392FF-3688-414B-A50D-453245A2C418");
    }
}

