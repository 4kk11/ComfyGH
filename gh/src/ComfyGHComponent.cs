using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RestSharp;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using GrasshopperAsyncComponent;

namespace ComfyGH
{
    public class ComfyGHComponent : GH_AsyncComponent
    {

        public ComfyGHComponent() : base("Comfy", "Comfy", "", "ComfyGH", "Main")
        {
            BaseWorker = new ComfyWorker(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ImagePath", "ImagePath", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "", GH_ParamAccess.item);
        }

        private class ComfyWorker : WorkerInstance
        {
            bool run;
            string input_image;
            string output_image;
            public ComfyWorker(GH_Component _parent) : base(_parent)
            {
            }

            public override WorkerInstance Duplicate()
            {
                return new ComfyWorker(Parent);
            }

            public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
            {
                DA.GetData(0, ref input_image);
                DA.GetData(1, ref run);
            }

            public override void SetData(IGH_DataAccess DA)
            {
                if(CancellationToken.IsCancellationRequested) return;
                DA.SetData(0, output_image);
            }

            public override void DoWork(Action<string, double> ReportProgress, Action Done)
            {
                if (run)
                {
                    string base64Image = Convert.ToBase64String(File.ReadAllBytes(this.input_image));

                    var client = new RestClient("http://127.0.0.1:8188");
                    var request = new RestRequest("/custom_nodes/ComfyGH/queue_prompt", Method.Post);
                    
                    var body = new { image = base64Image };
                    request.AddJsonBody(body);

                    var response = client.Execute(request);

                    Console.WriteLine(response.Content);
                    this.output_image = response.Content;
                }
                Done();
                
            }

        }


        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");
    }
}