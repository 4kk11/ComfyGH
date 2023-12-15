using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using RestSharp;
using Newtonsoft.Json;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using GrasshopperAsyncComponent;
using System.Text;

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
                string path = "";
                DA.GetData(0, ref path);
                input_image = CreateData(path);
                
                DA.GetData(1, ref run);
            }

            private string CreateData(string path)
            {
                var data = new Dictionary<string, string>{
                    ["type"] = "queue_prompt",
                    ["data"] = path,
                };

                return JsonConvert.SerializeObject(data);
            }

            public override void SetData(IGH_DataAccess DA)
            {
                if(CancellationToken.IsCancellationRequested) return;
                DA.SetData(0, output_image);
            }

            public override async void DoWork(Action<string, double> ReportProgress, Action Done)
            {
                if (run)
                {
                    using (var client = new ClientWebSocket())
                    {
                        try
                        {
                            Uri serverUri = new Uri("ws://127.0.0.1:8188/ws"); 
                            await client.ConnectAsync(serverUri, CancellationToken);

                            // Send to server
                            byte[] buffer = Encoding.UTF8.GetBytes(input_image);
                            await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken);

                            // Receive from server
                            var receiveBuffer = new byte[1024];
                            var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken);

                            // Convert to string
                            output_image = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                        }
                        catch
                        {
                            Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to connect to Comfy server. Make sure it's running.");
                            return;
                        }
                    }


                }
                Done();
                
            }

        }


        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");
    }
}