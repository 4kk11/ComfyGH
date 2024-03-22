using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Grasshopper.Kernel.Types;
using ComfyGH.Types;

namespace ComfyGH
{
    public static class ConnectionHelper
    {

        private static readonly string CLIENT_ID = "0CB33780A6EE4767A5DDC2AD41BFE975";
        private static readonly string SERVER_ADDRESS = "127.0.0.1:8188";
        public static async Task<List<ComfyNode>> GetGhNodesFromComfyUI(string url)
        {

            using (ClientWebSocket client = new ClientWebSocket())
            {
                // connect to websocket server
                string address = url.Replace("http://", "");
                Uri serverUri = new Uri($"ws://{address}/ws?clientId={CLIENT_ID}");
                await client.ConnectAsync(serverUri, CancellationToken.None);

                // create rest client
                RestClient restClient = new RestClient(url);
                RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/get_workflow", Method.GET);
                var body = new { text = "hello" };
                restRequest.AddJsonBody(body);
                await restClient.ExecuteAsync(restRequest);

                // receive from server
                Dictionary<string, object> data = null;
                while (client.State == WebSocketState.Open)
                {
                    var receiveBuffer = new byte[4096];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    // Convet to json
                    var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    var comfyReceiveObject = JsonConvert.DeserializeObject<ComfyReceiveObject>(json);

                    var type = comfyReceiveObject.Type;

                    if (type != "send_workflow") continue;
                    data = comfyReceiveObject.Data;
                    break;
                }

                var nodes = ((JArray)data["nodes"]).ToObject<List<ComfyNode>>();

                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                return nodes;
            }

        }



        public static async Task QueuePrompt(string url,
                                            Dictionary<string, SendingNodeInputData> sendingData,
                                            Action<Dictionary<string, object>> OnProgress,
                                            Action<Dictionary<string, object>> OnExecuted,
                                            Action<Dictionary<string, object>> OnClose)
        {
            using (var client = new ClientWebSocket())
            {
                // Connect to websocket server
                string address = url.Replace("http://", "");
                Uri serverUri = new Uri($"ws://{address}/ws?clientId={CLIENT_ID}");
                await client.ConnectAsync(serverUri, CancellationToken.None);

                // create rest client
                RestClient restClient = new RestClient(url);
                // Send to http server
                RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/queue_prompt", Method.POST);
                string jsonData = JsonConvert.SerializeObject(sendingData);
                restRequest.AddParameter("application/json", jsonData, ParameterType.RequestBody);
                restClient.Execute(restRequest);

                //Receive from server
                while (client.State == WebSocketState.Open)
                {
                    var receiveBuffer = new byte[1024];
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    // Convet to json
                    var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    var comfyReceiveObject = JsonConvert.DeserializeObject<ComfyReceiveObject>(json);

                    var type = comfyReceiveObject.Type;
                    var data = comfyReceiveObject.Data;

                    bool isClose = false;

                    switch (type)
                    {
                        case "comfygh_progress":
                            OnProgress(data);
                            break;

                        case "comfygh_executed":
                            OnExecuted(data);
                            break;

                        case "comfygh_close":
                            OnClose(data);
                            Console.WriteLine("Close!!!");
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
        }

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
    public class SendingNodeInputData
    {
        [JsonProperty("type")]
        public string NodeType { get; set; }

        [JsonProperty("value")]
        public object InputData { get; set; }

        private SendingNodeInputData() { }

        static public SendingNodeInputData Create(string nodeType, IGH_Goo data)
        {
            object inputData;
            switch (data)
            {
                case GH_ComfyImage image:
                    inputData = image.Value.ToBase64String();
                    break;
                case GH_String str:
                    inputData = str.Value;
                    break;
                default:
                    throw new Exception("Invalid data input type");
            }

            return new SendingNodeInputData
            {
                NodeType = nodeType,
                InputData = inputData
            };
        }
    }
}