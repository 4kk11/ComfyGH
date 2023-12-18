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
using System.Collections;
using ComfyGH.Params;
using ComfyGH.Types;
using System.Drawing;

namespace ComfyGH
{
    public class ComfyGHComponent : GH_AsyncComponent
    {
        string output_image;
        public ComfyGHComponent() : base("Comfy", "Comfy", "", "ComfyGH", "Main")
        {
            BaseWorker = new ComfyWorker(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_ComfyImage(), "Image", "Image", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "", GH_ParamAccess.item);
        }

        private class ComfyWorker : WorkerInstance
        {
            bool run;
            ComfyImage input_image;
            public ComfyWorker(GH_Component _parent) : base(_parent)
            {
            }

            public override WorkerInstance Duplicate()
            {
                return new ComfyWorker(Parent);
            }

            public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
            {
                GH_ComfyImage gH_ComfyImage = null;
                DA.GetData(0, ref gH_ComfyImage);
                
                if(gH_ComfyImage != null)
                     input_image = new ComfyImage(gH_ComfyImage.Value);
                DA.GetData(1, ref run);
            }

            public override void SetData(IGH_DataAccess DA)
            {
                DA.SetData(0, ((ComfyGHComponent)Parent).output_image);
            }

            public override async void DoWork(Action<string, double> ReportProgress, Action Done)
            {
                if (run)
                {
                    using (var client = new ClientWebSocket())
                    {
                        try
                        {
                            // Connect to websocket server
                            Uri serverUri = new Uri("ws://127.0.0.1:8188/ws?clientId=0CB33780A6EE4767A5DDC2AD41BFE975");
                            await client.ConnectAsync(serverUri, CancellationToken);
                        

                            // create rest client
                            RestClient restClient = new RestClient("http://127.0.0.1:8188");
                            // Send to http server
                            PostQueuePrompt(restClient, input_image);

                            // Receive from server
                            while(client.State == WebSocketState.Open)
                            {
                                var receiveBuffer = new byte[1024];
                                var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                                // Convet to json
                                var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                                var comfyReceiveObject = JsonConvert.DeserializeObject<ComfyReceiveObject>(json);

                                var type = comfyReceiveObject.Type;
                                var data = comfyReceiveObject.Data;
                                
                                bool isClose = false;

                                switch(type)
                                {
                                    case "comfygh_progress":
                                        var value = Convert.ToInt32(data["value"]);
                                        var max = Convert.ToInt32(data["max"]);
                                        ReportProgress(Id, (double)value / max);
                                        break;

                                    case "comfygh_executed":
                                        ((ComfyGHComponent)Parent).output_image = (string)data["image"];
                                        isClose = true;
                                        break;

                                    case "comfygh_close":
                                        isClose = true;
                                        break;
                                }
                                

                                if (isClose)
                                {
                                    // Close websocket
                                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                                    break;
                                }
                            }
                            
                        }
                        catch(Exception e)
                        {
                            Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                            return;
                        }
                    }
                    Done();
                }
                

            }

            private void PostQueuePrompt(RestClient restClient, ComfyImage image)
            {
                Bitmap bitmap = image.bitmap;
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bytes = stream.ToArray();
                    string base64Image = Convert.ToBase64String(bytes);
                    RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/queue_prompt", Method.POST);
                    var body = new { image = base64Image };
                    restRequest.AddJsonBody(body);
                    restClient.Execute(restRequest);
                }
            }

        }


        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");
    }


    public class ComfyReceiveObject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; }
    }
}