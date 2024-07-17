


using System;
using Grasshopper.Kernel;

namespace ComfyGH.Components
{
    public class SaveComfyImageComponent : GH_Component
    {
        public SaveComfyImageComponent() : base("SaveComfyImage", "SaveComfyImage", "", "ComfyGH", "Main")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Params.Param_ComfyImage(), "Image", "Image", "", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "Path", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Save", "Save", "", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // get data
            ComfyImage image = null;
            string path = "";
            bool save = false;

            DA.GetData("Image", ref image);
            DA.GetData("Path", ref path);
            DA.GetData("Save", ref save);

            if (save)
            {
                image.bitmap.Save(path);
            }
        }

        public override Guid ComponentGuid => new Guid("451799D2-504A-4E2B-9485-1CD68DBB5957");
    }

}