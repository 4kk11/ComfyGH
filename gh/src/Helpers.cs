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

namespace ComfyGH
{
    public static class Helpers
    {

        private static readonly string CLIENT_ID = "0CB33780A6EE4767A5DDC2AD41BFE975";
        private static readonly string SERVER_ADDRESS = "127.0.0.1:8188";
        public static async Task<List<ComfyNode>> GetGhNodesFromComfyUI()
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



        public static async Task QueuePrompt(Dictionary<string, SendingData> sendingData,
                                            Action<Dictionary<string, object>> OnProgress,
                                            Action<Dictionary<string, object>> OnExecuted,
                                            Action<Dictionary<string, object>> OnClose)
        {
            using (var client = new ClientWebSocket())
            {
                // Connect to websocket server
                Uri serverUri = new Uri($"ws://{SERVER_ADDRESS}/ws?clientId={CLIENT_ID}");
                await client.ConnectAsync(serverUri, CancellationToken.None);

                // create rest client
                RestClient restClient = new RestClient($"http://{SERVER_ADDRESS}");
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
                            isClose = true;
                            break;

                        case "comfygh_close":
                            OnClose(data);
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

        private static void PostQueuePrompt(RestClient restClient, Dictionary<string, SendingData> data)
        {
            RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/queue_prompt", Method.POST);
            string jsonData = JsonConvert.SerializeObject(data);
            restRequest.AddParameter("application/json", jsonData, ParameterType.RequestBody);
            restClient.Execute(restRequest);
        }

    }
}