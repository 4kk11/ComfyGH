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
using System.Web.ModelBinding;
using System.CodeDom;
using Grasshopper.Kernel.Types;

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
            bool updateParams = false;
            DA.GetData(1, ref updateParams);
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

        private NodeDictionary InputNodeDic = new NodeDictionary();
        private NodeDictionary OutputNodeDic = new NodeDictionary();

        private void UpdateParameters()
        {
            GH_ComponentParamServer.IGH_SyncObject sync_data = base.Params.EmitSyncObject();

            // Unregist
            Dictionary<string, IEnumerable<IGH_Param>> idSourcesPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach(var id in InputNodeDic.Keys)
            {
                IGH_Param param = InputNodeDic.GetParam(id);
                idSourcesPairs.Add(id, param.Sources.ToList()); // copy sources list
                Params.UnregisterInputParameter(param);
                InputNodeDic.Remove(id);
            }

            Dictionary<string, IEnumerable<IGH_Param>> idRecipientsPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach(var id in OutputNodeDic.Keys)
            {
                IGH_Param param = OutputNodeDic.GetParam(id);
                idRecipientsPairs.Add(id, param.Recipients.ToList());
                Params.UnregisterOutputParameter(param);
                OutputNodeDic.Remove(id);
            }
            
            // Regist
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
                    case "GH_Text":
                        param = new Param_String();
                        isInput = true;
                        break;
                    default:
                        continue;
                }

                param.Name = nickname;
                param.NickName = nickname;
                if(isInput)
                {
                    param.Access = GH_ParamAccess.item;
                    param.Optional = true;
                    InputNodeDic.Add(node.Id, param, node);
                    Params.RegisterInputParam(param);
                }
                else
                {
                    param.Access = GH_ParamAccess.item;
                    OutputNodeDic.Add(node.Id, param, node);
                    Params.RegisterOutputParam(param);   
                }
            }
            
            // Restoration sources
            foreach(var id in InputNodeDic.Keys)
            {
                var param = InputNodeDic.GetParam(id);
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
            foreach(var id in OutputNodeDic.Keys)
            {
                var param = OutputNodeDic.GetParam(id);
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

            Dictionary<string, SendingData> inputData = new Dictionary<string, SendingData>();
            public ComfyWorker(GH_Component _parent) : base(_parent)
            {
            }

            public override WorkerInstance Duplicate()
            {
                return new ComfyWorker(Parent);
            }

            public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
            {
                DA.GetData(0, ref run);

                // Get data from input node params
                ((ComfyGHComponent)Parent).InputNodeDic.ToList().ForEach(pair => {
                    var id = pair.Key;
                    var nodeInfo = pair.Value;
                    var param = nodeInfo.Parameter;
                    object data = null;
                    DA.GetData(param.Name, ref data);
                    inputData.Add(id, new SendingData{
                        Type = nodeInfo.Node.Type,
                        Data = data
                    });
                });
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
                            var serializeData = SerializeData(inputData);
                            PostQueuePrompt(restClient, serializeData);

                            //Receive from server
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

            private void PostQueuePrompt(RestClient restClient, Dictionary<string, SendingData> data)
            {
                RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/queue_prompt", Method.POST);
                string jsonData = JsonConvert.SerializeObject(data);
                restRequest.AddParameter("application/json", jsonData, ParameterType.RequestBody);
                restClient.Execute(restRequest);
            }

            private Dictionary<string, SendingData> SerializeData(Dictionary<string, SendingData> data)
            {
                var serializeData = new Dictionary<string, SendingData>();
                foreach(var pair in data)
                {
                    string key = pair.Key;
                    SendingData value = pair.Value;
                    serializeData.Add(key, SerializeData(value));
                }
                return serializeData;
            }

            private SendingData SerializeData(SendingData data)
            {
                return new SendingData{
                    Type = data.Type,
                    Data = SerializeData(data.Data)
                };
            }

            private string SerializeData(object data)
            {
                if(data is GH_ComfyImage image)
                {
                    lock(ImagePreviewAttributes.bitmapLock)
                    {
                        using(MemoryStream stream = new MemoryStream())
                        {
                            image.Value.bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            byte[] bytes = stream.ToArray();
                            string base64Image = Convert.ToBase64String(bytes);
                            return base64Image;
                        }
                    }
                }
                else if(data is GH_String gH_String)
                {
                    return gH_String.Value;
                }

                return "";
            }

        }


        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");
    }


    // ComfyUIから送られてくるデータクラス
    public class ComfyReceiveObject
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; }
    }


    // ComfyReceiveObjectのobjectの中身
    public class ComfyNode
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("nickname")]
        public string Nickname { get; set; }
    }


    // ghコンポーネントに入力されたデータをConfyUIに送るためのデータクラス
    public class SendingData
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public object Data { get; set; }
    }

    
    // ComfyUIのノードとghコンポーネントのパラメータを紐づけるためのクラス
    public class NodeParameterInfo
    {
        public IGH_Param Parameter {get;}
        public ComfyNode Node {get;}

        public NodeParameterInfo(IGH_Param parameter, ComfyNode node)
        {
            Parameter = parameter;
            Node = node;
        }
    }


    // NodeParameterInfoとComfyUIのノードidを紐づけるためのクラス
    public class NodeDictionary: Dictionary<string, NodeParameterInfo>
    {
        public void Add(string key, IGH_Param param, ComfyNode node)
        {
            this[key] = new NodeParameterInfo(param, node);
        }

        public bool TryGetValue(string key, out IGH_Param param, out ComfyNode node)
        {
            if (this.TryGetValue(key, out var value))
            {
                param = value.Parameter;
                node = value.Node;
                return true;
            }

            param = null;
            node = null;
            return false;
        }

        public IGH_Param GetParam(string key)
        {
            return this[key].Parameter;
        }

        public ComfyNode GetNode(string key)
        {
            return this[key].Node;
        }
    }
}