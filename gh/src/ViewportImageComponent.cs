using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

using Rhino;
using Rhino.Geometry;
using Rhino.Display;
using Rhino.DocObjects;

using Grasshopper;
using Grasshopper.Kernel;

using ComfyGH.Params;
using ComfyGH.Types;




namespace ComfyGH
{
    public class ViewportImageComponent : GH_Component
    {
        private GH_ComfyImage _image;
        public ViewportImageComponent() : base("ViewportImage", "ViewportImage", "", "ComfyGH", "Main")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Update", "Update", "", GH_ParamAccess.item, false);
            this.Params.Input[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_ComfyImage(), "Image", "Image", "", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //get data
            bool update = false;
            DA.GetData(0, ref update);

            if (update)
            {
                this.AddHandlers();

                RhinoDoc doc = RhinoDoc.ActiveDoc;
                RhinoViewport viewport = doc.Views.ActiveView.ActiveViewport;

                Bitmap bitmap = DisplayPipeline.DrawToBitmap(viewport, viewport.Size.Width, viewport.Size.Height);
                ComfyImage comfyImage = new ComfyImage(bitmap);
                GH_ComfyImage image = new GH_ComfyImage(comfyImage);
                _image = image;

                DA.SetData(0, _image);
            }
            else
            {
                this.RemoveHandlers();
                DA.SetData(0, _image);
            }

        }

        // public override void AddedToDocument(GH_Document document)
        // {
        //     base.AddedToDocument(document);
        //     this.AddHandlers();
        // }

        public override void RemovedFromDocument(GH_Document document)
        {
            base.RemovedFromDocument(document);
            this.RemoveHandlers();
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            base.DocumentContextChanged(document, context);
            switch (context)
            {
                case GH_DocumentContext.Loaded:
                case GH_DocumentContext.Unlock:
                case GH_DocumentContext.Open:
                    this.AddHandlers();
                    break;
                case GH_DocumentContext.Close:
                case GH_DocumentContext.Unloaded:
                case GH_DocumentContext.Lock:
                    this.RemoveHandlers();
                    break;
            }
        }

        private void AddHandlers()
        {
            this.RemoveHandlers();

            RhinoView.Modified += OnViewModified;
            RhinoView.Create += OnViewModified;
            RhinoView.Destroy += OnViewModified;

            RhinoDoc.AddRhinoObject += OnObjectAdded;
            RhinoDoc.DeleteRhinoObject += OnObjectDeleted;
            RhinoDoc.DeselectAllObjects += OnObjectsDeselected;

        }

        private void RemoveHandlers()
        {
            RhinoView.Modified -= OnViewModified;
            RhinoView.Create -= OnViewModified;
            RhinoView.Destroy -= OnViewModified;

            RhinoDoc.AddRhinoObject -= OnObjectAdded;
            RhinoDoc.DeleteRhinoObject -= OnObjectDeleted;
            RhinoDoc.DeselectAllObjects -= OnObjectsDeselected;
        }


        private bool _ExpireScheduled_View;
        private void OnViewModified(object sender, ViewEventArgs e)
        {
            if (_ExpireScheduled_View) return;
            GH_Document ghdoc = this.OnPingDocument();
            if (ghdoc != null)
            {
                _ExpireScheduled_View = true;
                ghdoc.ScheduleSolution((ghdoc.SolutionDepth == 0) ? 10 : 1000, delegate
                {
                    _ExpireScheduled_View = false;
                    this.ExpireSolution(false);
                });
            }
        }

        private bool _ExpireScheduled_Add;
        private void OnObjectAdded(object sender, RhinoObjectEventArgs e)
        {
            if (_ExpireScheduled_Add) return;
            GH_Document ghdoc = this.OnPingDocument();
            if (ghdoc != null)
            {
                _ExpireScheduled_Add = true;
                ghdoc.ScheduleSolution((ghdoc.SolutionDepth == 0) ? 100 : 1000, delegate
                {
                    _ExpireScheduled_Add = false;
                    this.ExpireSolution(false);
                });
            }
        }

        private bool _ExpireScheduled_Delete;
        private void OnObjectDeleted(object sender, RhinoObjectEventArgs e)
        {
            if (_ExpireScheduled_Delete) return;
            GH_Document ghdoc = this.OnPingDocument();
            if (ghdoc != null)
            {
                _ExpireScheduled_Delete = true;
                ghdoc.ScheduleSolution((ghdoc.SolutionDepth == 0) ? 100 : 1000, delegate
                {
                    _ExpireScheduled_Delete = false;
                    this.ExpireSolution(false);
                });
            }
        }

        private void OnObjectsDeselected(object sender, RhinoDeselectAllObjectsEventArgs e)
        {
            this.ExpireSolution(true);
        }

        public override Guid ComponentGuid => new Guid("2C0EA698-BF1F-44F4-88E5-EBCB8074040A");
    }
}