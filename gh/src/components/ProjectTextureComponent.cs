using System;
using System.Linq;
using ComfyGH.Params;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Runtime;

namespace ComfyGH.Components
{
    public class ProjectTextureComponent : GH_Component
    {
        public ProjectTextureComponent() : base("ProjectTexture", "ProjectTexture", "", "ComfyGH", "Main")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_ComfyImage(), "Image", "Image", "", GH_ParamAccess.item);
            pManager.AddGenericParameter("Guid", "Guid", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Update", "Update", "", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //get data
            ComfyImage image = null;
            Guid guid = Guid.Empty;
            bool update = false;
            DA.GetData("Image", ref image);
            DA.GetData("Guid", ref guid);
            DA.GetData("Update", ref update);

            if(!update) return;
            
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            
            // get mesh
            RhinoObject ro = doc.Objects.Find(guid);
            Mesh mesh = ro.Geometry as Mesh;
            if(mesh == null) 
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Geometry is not mesh");
                return;
            }

            RhinoViewport viewport = doc.Views.ActiveView.ActiveViewport;
 
            // project texture
            this.ProjectTextureToMesh(mesh, viewport);

            // set material
            this.SetMaterial(ro, image, doc);
            ro.CommitChanges();

        }

        private void ProjectTextureToMesh(Mesh mesh, RhinoViewport viewport)
        {
            ViewportInfo viewportInfo = new ViewportInfo(viewport);
            Transform xform = viewportInfo.GetXform(CoordinateSystem.World, CoordinateSystem.Screen);

            var size = viewport.ParentView.DisplayPipeline.FrameSize;

            mesh.TextureCoordinates.Clear();
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Point3d p = mesh.Vertices[i];
                p.Transform(xform);
                double u = p.X / (double)size.Width;
                double v = 1.0 - p.Y / (double)size.Height;
                mesh.TextureCoordinates.Add(u, v);
            }

        }

        private void SetMaterial(RhinoObject ro, ComfyImage image, RhinoDoc doc)
        {
            var renderTexture = Rhino.Render.RenderTexture.NewBitmapTexture(image.bitmap, doc);
            Texture texture = renderTexture.SimulatedTexture(Rhino.Render.RenderTexture.TextureGeneration.Allow).Texture();

            Material material = new Material();
            material.Name = "mat";
            material.SetBitmapTexture(texture);
            RenderMaterial rm = RenderMaterial.CreateBasicMaterial(material, doc);
            doc.RenderMaterials.Add(rm);

            ro.RenderMaterial = rm;
        }


        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("0616E759-6A76-4FA2-8C35-79AD1BCD01E0");
    }
}