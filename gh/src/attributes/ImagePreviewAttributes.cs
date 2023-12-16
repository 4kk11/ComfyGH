using System;
using System.Drawing;
using ComfyGH.Params;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using System.Windows.Forms;

// Grasshopper用のカスタムコンポーネントの属性を定義するための名前空間。
namespace ComfyGH.Attributes
{
    // Param_ComfyImageパラメータのためのカスタム属性クラスを定義します。
    public class ImagePreviewAttributes : GH_ResizableAttributes<Param_ComfyImage>
    {
        // 最小サイズを定義します。この場合、50x50ピクセルです。
        protected override Size MinimumSize => new Size(50, 50);

        // サイズ変更のためのボーダーの幅を定義します。ここでは5ピクセルです。
        protected override Padding SizingBorders => new Padding(5);

        // コンストラクタ。Param_ComfyImageインスタンスを受け取り、基本クラスのコンストラクタに渡します。
        public ImagePreviewAttributes(Param_ComfyImage owner): base(owner)
        {
            // 初期のバウンドを設定します。ここでは100x100ピクセルの正方形です。
            Bounds = new Rectangle(0, 0, 100, 100);
        }

        // オブジェクトの描画を担当するメソッド。
        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            // 描画チャネルに基づいて異なる描画動作をします。
            switch (channel)
            {
            // ワイヤー（接続線）の描画。
            case GH_CanvasChannel.Wires:
                if (base.Owner.SourceCount > 0)
                {
                    RenderIncomingWires(canvas.Painter, base.Owner.Sources, base.Owner.WireDisplay);
                }
                break;
            // オブジェクトの描画。
            case GH_CanvasChannel.Objects:
            {
                // カプセル（コンポーネントの外見）を作成し、描画します。
                GH_Capsule gH_Capsule = GH_Capsule.CreateCapsule(Bounds, GH_Palette.Hidden, 5, 0);
                gH_Capsule.AddInputGrip(InputGrip);
                gH_Capsule.AddOutputGrip(OutputGrip);
                gH_Capsule.Render(graphics, Selected, base.Owner.Locked, hidden: false);
                gH_Capsule.Dispose();
                
                // 画像を描画するための領域を計算します。
                Rectangle rect = GH_Convert.ToRectangle(Bounds);
                rect.Inflate(-5, -5);
                
                Bitmap image;
                ImageUriImageLoading cachedPreview = base.Owner.GetCachedPreview(out image);

                // キャッシュされた画像の状態に基づいて異なる動作をします。
                switch (cachedPreview)
                {
                case ImageUriImageLoading.BusyLoading:
                    // 画像がまだ読み込み中の場合、再描画をスケジュールします。
                    canvas.ScheduleRegen(100);
                    break;
                case ImageUriImageLoading.FinishedLoading:
                    // 画像が読み込み済みの場合、それを描画します。
                    graphics.DrawImage(image, rect);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unrecognised cache value: {cachedPreview}");
                case ImageUriImageLoading.None:
                    // 画像がない場合、特に何もしません。
                    break;
                }

                // 領域の枠を描画します。
                using (Pen pen = new Pen(Color.Black))
                {
                    graphics.DrawRectangle(pen, rect);
                }
                break;
            }
            }
        }
    }

    // 画像の読み込み状態を表す列挙型。
    public enum ImageUriImageLoading
    {
        None,           // 画像なし
        BusyLoading,    // 読み込み中
        FinishedLoading // 読み込み完了
    }
}
