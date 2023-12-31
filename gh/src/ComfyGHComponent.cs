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
using ComfyGH.Attributes;
using Newtonsoft.Json.Linq;
using Grasshopper.Kernel.Parameters;
using Rhino;
using System.Linq;
using Grasshopper.Kernel.Geometry;

namespace ComfyGH
{
    public class ComfyGHComponent : GH_AsyncComponent
    {
        string output_image;
        private static readonly string CLIENT_ID = "0CB33780A6EE4767A5DDC2AD41BFE975";
        private static readonly string SERVER_ADDRESS = "127.0.0.1:8188";
        public ComfyGHComponent() : base("Comfy", "Comfy", "", "ComfyGH", "Main")
        {
            BaseWorker = new ComfyWorker(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_ComfyImage(), "Image", "Image", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "", GH_ParamAccess.item);
            pManager.AddBooleanParameter("UpdateParams", "UpdateParams", "", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "", GH_ParamAccess.item);
        }

        private List<ComfyNode> nodes;
        protected override async void SolveInstance(IGH_DataAccess DA)
        {
            Console.WriteLine("BeforeSolveInstance");
            bool updateParams = false;
            DA.GetData(2, ref updateParams);
            if(updateParams)
            {
                var nodes = await GetNodes();
                this.nodes = nodes;
                OnPingDocument().ScheduleSolution(1, SolutionCallback);
            }

            base.SolveInstance(DA);
        }

        private async Task<List<ComfyNode>> GetNodes()
        {
            using (ClientWebSocket client = new ClientWebSocket())
            {
                // connect to websocket server
                Uri serverUri = new Uri($"ws://{SERVER_ADDRESS}/ws?clientId={CLIENT_ID}");
                await client.ConnectAsync(serverUri, CancellationToken.None);

                // create rest client
                RestClient restClient = new RestClient($"http://{SERVER_ADDRESS}");
                RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/get_workflow", Method.GET);
                var body = new { text = "hello" };
                restRequest.AddJsonBody(body);
                await restClient.ExecuteAsync(restRequest);

                // receive from server
                Dictionary<string, object> data = null;
                while(client.State == WebSocketState.Open)
                {
                    var receiveBuffer = new byte[4096];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    // Convet to json
                    var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    Console.WriteLine(json);
                    var comfyReceiveObject = JsonConvert.DeserializeObject<ComfyReceiveObject>(json);

                    var type = comfyReceiveObject.Type;
                    
                    if(type != "send_workflow") continue;
                    data = comfyReceiveObject.Data;
                    break;
                }

                var nodes = ((JArray)data["nodes"]).ToObject<List<ComfyNode>>();

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                return nodes;
            }
        }

        private Dictionary<string, IGH_Param> nodeIdInputParamPairs = new Dictionary<string, IGH_Param>();
        private Dictionary<string, IGH_Param> nodeIdOutputParamPairs = new Dictionary<string, IGH_Param>();

        private void UpdateParameters()
        {
            GH_ComponentParamServer.IGH_SyncObject sync_data = base.Params.EmitSyncObject();

            // Unregist
            Dictionary<string, IEnumerable<IGH_Param>> idSourcesPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach(var id in nodeIdInputParamPairs.Keys)
            {
                var intpuParam = nodeIdInputParamPairs[id];
                idSourcesPairs.Add(id, intpuParam.Sources.ToList()); // copy sources list
                Params.UnregisterInputParameter(intpuParam);
                nodeIdInputParamPairs.Remove(id);
            }

            Dictionary<string, IEnumerable<IGH_Param>> idRecipientsPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach(var id in nodeIdOutputParamPairs.Keys)
            {
                var outputParam = nodeIdOutputParamPairs[id];
                idRecipientsPairs.Add(id, outputParam.Recipients.ToList());
                Params.UnregisterOutputParameter(outputParam);
                nodeIdOutputParamPairs.Remove(id);
            }
            
            // Regist input
            foreach(var node in this.nodes)
            {
                var nickname = node.Nickname;
                var type = node.Type;

                IGH_Param param;
                bool isInput = false;
                switch(type){
                    case "GH_LoadImage":
                        param = new Param_ComfyImage();
                        isInput = true;
                        break;
                    case "GH_PreviewImage":
                        param = new Param_String();
                        break;
                    default:
                        continue;
                }

                param.Name = nickname;
                param.NickName = nickname;
                if(isInput)
                {

                    param.Access = GH_ParamAccess.tree;
                    param.Optional = true;
                    nodeIdInputParamPairs.Add(node.Id, param);
                    Params.RegisterInputParam(param);
                }
                else
                {
                    param.Access = GH_ParamAccess.item;
                    nodeIdOutputParamPairs.Add(node.Id, param);
                    Params.RegisterOutputParam(param);   
                }
            }
            
            // Restoration sources
            foreach(var id in nodeIdInputParamPairs.Keys)
            {
                var param = nodeIdInputParamPairs[id];
                if(idSourcesPairs.ContainsKey(id))
                {
                    var sources = idSourcesPairs[id];
                    foreach(var source in sources)
                    {
                        param.AddSource(source);
                    }
                }
            }

            // Restoration recipients
            foreach(var id in nodeIdOutputParamPairs.Keys)
            {
                var param = nodeIdOutputParamPairs[id];
                if(idRecipientsPairs.ContainsKey(id))
                {
                    var recipients = idRecipientsPairs[id];
                    foreach(var recipient in recipients)
                    {
                        recipient.AddSource(param);
                    }
                }
            }


            base.Params.Sync(sync_data);
            OnAttributesChanged();
        }

        private void SolutionCallback(GH_Document doc)
        {
            this.UpdateParameters();
            ExpireSolution(false);
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
                {
                    //input_image = new ComfyImage(gH_ComfyImage.Value);
                     input_image = gH_ComfyImage.Value;
                }

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
                            Uri serverUri = new Uri($"ws://{SERVER_ADDRESS}/ws?clientId={CLIENT_ID}");
                            await client.ConnectAsync(serverUri, CancellationToken);


                            // create rest client
                            RestClient restClient = new RestClient($"http://{SERVER_ADDRESS}");
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
                lock(ImagePreviewAttributes.bitmapLock)
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

    public class ComfyNode
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("nickname")]
        public string Nickname { get; set; }
    }
}