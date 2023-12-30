using System;
using System.Drawing;
using ComfyGH.Params;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using System.Windows.Forms;

namespace ComfyGH.Attributes
{
    public class ImagePreviewAttributes : GH_ResizableAttributes<Param_ComfyImage>
    {
        public static readonly object bitmapLock = new object();
        protected override Size MinimumSize => new Size(50, 50);

        // This is Border Padding for changing the size.
        protected override Padding SizingBorders => new Padding(5);

        public ImagePreviewAttributes(Param_ComfyImage owner) : base(owner)
        {
            // Initialize the bounds of the UI.
            Bounds = new Rectangle(0, 0, 100, 100);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            switch (channel)
            {
                case GH_CanvasChannel.Wires:
                    if (base.Owner.SourceCount > 0)
                    {
                        // Render incoming wires
                        RenderIncomingWires(canvas.Painter, base.Owner.Sources, base.Owner.WireDisplay);
                    }
                    break;
         
                case GH_CanvasChannel.Objects:
                    {
                        // Render capsule 
                        GH_Capsule gH_Capsule = GH_Capsule.CreateCapsule(Bounds, GH_Palette.Hidden, 0, 0);
                        gH_Capsule.AddInputGrip(InputGrip);
                        gH_Capsule.AddOutputGrip(OutputGrip);
                        gH_Capsule.Render(graphics, Selected, base.Owner.Locked, hidden: false);
                        gH_Capsule.Dispose();

                        // Render preview image
                        Rectangle rect = GH_Convert.ToRectangle(Bounds);
                        Bitmap image = base.Owner.LoadPreviewImage();
                        if (image != null)
                        {
                            lock(bitmapLock)
                            {
                                graphics.DrawImage(image, rect);
                            }
                        }

                        break;
                    }
            }
        }
    }


}
