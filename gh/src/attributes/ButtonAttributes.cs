using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino.UI.Controls;

namespace ComfyGH.Attributes
{
    public enum RunningState
    {
        Idle,
        Running,
        Finished,
        Error
    }

    public class ButtonAttributes : GH_ComponentAttributes
    {
        private Rectangle ButtonBounds { get; set; }

        public bool Pressed { get; private set; }

        private string DisplayText => "Run";

        public bool Visible { get; set; } = true;

        public bool Enabled { get; set; } = true;

        public RunningState RunningState { get; set; } = RunningState.Idle;

        public ButtonAttributes(IGH_Component component) : base(component)
        {
        }

        protected override void Layout()
        {
            base.Layout();
            if (this.Visible)
            {
                Rectangle rectangle = GH_Convert.ToRectangle(this.Bounds);
                rectangle.Height += 22;
                Rectangle buttonBounds = rectangle;
                buttonBounds.Y = buttonBounds.Bottom - 22;
                buttonBounds.Height = 22;
                buttonBounds.Inflate(-2, -2);
                this.Bounds = rectangle;
                this.ButtonBounds = buttonBounds;
            }
            else
            {
                this.ButtonBounds = Rectangle.Empty;
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {

            if (channel == GH_CanvasChannel.Objects)
            {
                GH_PaletteStyle style = GH_Skin.palette_normal_standard;

                Color edgeColor;
                switch (this.RunningState)
                {
                    case RunningState.Idle:
                        edgeColor = Color.Black;
                        break;
                    case RunningState.Running:
                        edgeColor = Color.FromArgb(0, 122, 204);
                        break;
                    case RunningState.Finished:
                        edgeColor = Color.FromArgb(0, 204, 0);
                        break;
                    case RunningState.Error:
                        edgeColor = Color.FromArgb(204, 0, 0);
                        break;
                    default:
                        edgeColor = Color.Black;
                        break;
                }
                //Color textColor = GH_GraphicsUtil.ForegroundColour(edgeColor, 200);
                GH_Skin.palette_normal_standard = new GH_PaletteStyle(style.Fill, edgeColor, style.Text);
                base.Render(canvas, graphics, channel);

                GH_Skin.palette_normal_standard = style;

                if (this.Visible && channel == GH_CanvasChannel.Objects)
                {
                    if (this.Enabled)
                    {
                        using (GH_Capsule gH_Capsule = (this.Pressed ? GH_Capsule.CreateTextCapsule(this.ButtonBounds, this.ButtonBounds, GH_Palette.Grey, DisplayText, 2, 0) :
                                                                    GH_Capsule.CreateTextCapsule(this.ButtonBounds, this.ButtonBounds, GH_Palette.Black, DisplayText, 2, 0)))
                        {
                            gH_Capsule.Render(graphics, this.Selected, base.Owner.Locked, hidden: false);
                        }
                    }
                    else
                    {
                        using (GH_Capsule gH_Capsule = GH_Capsule.CreateTextCapsule(this.ButtonBounds, this.ButtonBounds, GH_Palette.Locked, DisplayText, 2, 0))
                        {
                            gH_Capsule.Render(graphics, this.Selected, base.Owner.Locked, hidden: false);
                        }
                    }
                }
            }
            else
            {
                base.Render(canvas, graphics, channel);
            }

        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (this.Enabled && this.Visible && e.Button == MouseButtons.Left && ((RectangleF)this.ButtonBounds).Contains(e.CanvasLocation))
            {
                this.Pressed = true;
                sender.Refresh();
                return GH_ObjectResponse.Capture;
            }

            return base.RespondToMouseDown(sender, e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (this.Enabled && this.Pressed)
            {
                if (((RectangleF)this.ButtonBounds).Contains(e.CanvasLocation))
                {
                    GH_Document gH_Document = base.Owner.OnPingDocument();
                    if (gH_Document != null)
                    {
                        // GH_Document.SolutionEndEventHandler SolutionEnd = null;
                        // gH_Document.SolutionEnd += (SolutionEnd = delegate(object s, GH_SolutionEventArgs args)
                        // {
                        //     (s as GH_Document).SolutionEnd -= SolutionEnd;
                        //     this.Pressed = false;
                        // });
                        base.Owner.ExpireSolution(true);
                    }
                }
                this.Pressed = false;
                sender.Refresh();
                return GH_ObjectResponse.Release;
            }
            return base.RespondToMouseUp(sender, e);
        }

    }
}