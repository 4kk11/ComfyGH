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
using System.Diagnostics;

namespace ComfyGH
{
    public static class ConnectionHelper
    {

        public static bool ValidateComfyGHConnection(string url)
        {
            try
            {
                RestClient restClient = new RestClient(url);
                restClient.Timeout = 2000;
                RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/validate_connection", Method.GET);
                var response = restClient.Execute(restRequest);
                return response.IsSuccessful;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        public static List<ComfyNode> GetGhNodes(string url, string jsonPath)
        {
            RestClient restClient = new RestClient(url);
            RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/gh_nodes", Method.POST);
            restRequest.AddParameter("application/json", File.ReadAllText(jsonPath), ParameterType.RequestBody);
            IRestResponse response;
            try
            {
                response = restClient.Execute(restRequest);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new Exception("Failed to request in GetGhNodes");
            }

            if (!response.IsSuccessful)
            {
                var errorData = JObject.Parse(response.Content);
                throw new Exception(errorData["error"].ToString());
            }

            var data = JObject.Parse(response.Content);
            var nodes = data["nodes"].ToObject<List<ComfyNode>>();
            return nodes;
        }

        public static async Task<string> TranslateWorkflow(string url, ComfyWorkflow workflow)
        {
            RestClient restClient = new RestClient(url);
            RestRequest restRequest = new RestRequest("/custom_nodes/ComfyGH/prompt", Method.POST);
            restRequest.AddParameter("application/json", workflow.GetJsonObject(), ParameterType.RequestBody);
            IRestResponse response;
            try
            {
                response = await restClient.ExecuteAsync(restRequest);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw new Exception("Failed to request in TranslateWorkflow");
            }

            if (!response.IsSuccessful)
            {
                var errorData = JObject.Parse(response.Content);
                throw new Exception(errorData["error"].ToString());
            }

            return response.Content;
        }


        public static async Task QueuePrompt(string url, ComfyWorkflow workflow,
                                            Action<Dictionary<string, object>> OnStatus,
                                            Action<Dictionary<string, object>> OnProgress,
                                            Action<Dictionary<string, object>> OnExecuting,
                                            Action<Dictionary<string, object>> OnReceivedImage,
                                            Action<Dictionary<string, object>> OnReceivedMesh)
        {

            string _client_id = Guid.NewGuid().ToString("N").ToUpper();

            string _promptJsonString = await ConnectionHelper.TranslateWorkflow(url, workflow);

            JObject _workflowJson = workflow.GetJsonObject();
            JObject _promptJson = JObject.Parse(_promptJsonString);

            var jsonObject = new
            {
                client_id = _client_id,
                extra_data = new
                {
                    extra_pnginfo = new
                    {
                        workflow = _workflowJson,
                    }
                },
                prompt = _promptJson,
            };

            using (var client = new ClientWebSocket())
            {
                // Connect to websocket server
                string address = url.Replace("http://", "");
                Uri serverUri = new Uri($"ws://{address}/ws?clientId={_client_id}");
                await client.ConnectAsync(serverUri, CancellationToken.None);

                // create rest client
                RestClient restClient = new RestClient(url);
                // Send to http server
                RestRequest restRequest = new RestRequest("/prompt", Method.POST);
                string jsonData = JsonConvert.SerializeObject(jsonObject);
                restRequest.AddParameter("application/json", jsonData, ParameterType.RequestBody);
                restClient.Execute(restRequest);

                // Receive from server
                var receivedData = new List<byte>();
                while (client.State == WebSocketState.Open)
                {
                    var receiveBuffer = new byte[4096];
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                        receivedData.AddRange(new ArraySegment<byte>(receiveBuffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    // Convet to json
                    var json = Encoding.UTF8.GetString(receivedData.ToArray());

                    ComfyReceiveObject comfyReceiveObject = JsonConvert.DeserializeObject<ComfyReceiveObject>(json);

                    receivedData.Clear();

                    var type = comfyReceiveObject.Type;
                    var data = comfyReceiveObject.Data;

                    bool isClose = false;

                    switch (type)
                    {
                        case "status":
                            OnStatus(data);
                            break;
                        case "progress":
                            OnProgress(data);
                            break;
                        case "gh_send_image":
                            OnReceivedImage(data);
                            break;
                        case "gh_send_mesh":
                            OnReceivedMesh(data);
                            break;
                        case "executing":
                            var node = data["node"];
                            if (node == null)
                            {
                                Debug.WriteLine("Close");
                                isClose = true;
                                break;
                            }
                            OnExecuting(data);
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

}