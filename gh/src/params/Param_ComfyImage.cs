using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using ComfyGH.Types;

namespace ComfyGH.Params
{
    public class Param_ComfyImage: GH_Param<GH_ComfyImage>
    {
        public Param_ComfyImage(): base(new GH_InstanceDescription("ComfyImage", "CI", "ComfyImage", "ComfyGH", "Params"))
        {
        }


        public override GH_Exposure Exposure => GH_Exposure.primary;


        public override Guid ComponentGuid => new Guid("86A392FF-3688-414B-A50D-453245A2C418");
    }
}

