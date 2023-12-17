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

        public Param_ComfyImage() : base(new GH_InstanceDescription("ComfyImage", "CI", "ComfyImage", "ComfyGH", "Params"))
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new ImagePreviewAttributes(this);
        }

        public override void ExpireSolution(bool recompute)
        {
            base.ExpireSolution(recompute);
        }

        public Bitmap LoadPreviewImage()
        {
            if (m_data.IsEmpty) return null;

            List<ComfyImage> list = new List<ComfyImage>();
            foreach (IGH_Goo item in m_data.AllData(skipNulls: true))
            {
                if (item is GH_ComfyImage comfyImage)
                {
                    list.Add(comfyImage.Value);
                }
            }

            if (list.Count == 0) return null;

            Bitmap bitmap = list[0].bitmap;
            return bitmap;
        }


        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override Guid ComponentGuid => new Guid("86A392FF-3688-414B-A50D-453245A2C418");
    }
}

