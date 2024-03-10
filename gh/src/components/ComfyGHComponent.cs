using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Grasshopper;
using Grasshopper.Kernel;
using GrasshopperAsyncComponent;
using ComfyGH.Params;
using ComfyGH.Types;
using ComfyGH.Attributes;
using Grasshopper.Kernel.Parameters;
using System.Linq;

using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

namespace ComfyGH.Components
{
    public class ComfyGHComponent : GH_AsyncComponent
    {
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

        // ComfyUIから送られてくる画像のパスを格納するためにDictionary、keyはComfyUIのノードid
        Dictionary<string, string> outputImagesDic = new Dictionary<string, string>();

        // ComfyUIのキャンバス上にあるGHノードを格納するList
        private List<ComfyNode> ReceivedComfyNodes;

        // ComfyUIのノードとコンポーネントのパラメータを紐づけた情報
        private ComfyNodeGhParamLookup InputNodeDic = new ComfyNodeGhParamLookup();
        private ComfyNodeGhParamLookup OutputNodeDic = new ComfyNodeGhParamLookup();

        protected override async void SolveInstance(IGH_DataAccess DA)
        {
            bool updateParams = false;
            DA.GetData(1, ref updateParams);
            if (updateParams)
            {
                // ComfyUIからノードを取得し、それを元にコンポーネントのinput/outputを更新する
                try
                {
                    var nodes = await ConnectionHelper.GetGhNodesFromComfyUI();
                    this.ReceivedComfyNodes = nodes;
                    OnPingDocument().ScheduleSolution(1, UpdateParameters);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                }

                return;
            }

            base.SolveInstance(DA);

            // 生成したデータをOutputに保持させておく
            // これをしないと、再計算の際(F5キーとか)にデータが消えてしまう
            foreach (var output_image in outputImagesDic)
            {
                var id = output_image.Key;
                var imagePath = output_image.Value;
                bool isExist = OutputNodeDic.TryGetValue(id, out var param, out var node);
                if (!isExist) continue;
                DA.SetData(param.Name, imagePath);
            }
        }

        private void UpdateParameters(GH_Document doc)
        {
            // ConfyUIから受け取ったノード情報を元に、コンポーネントのinput/outputを更新する
            GhParamServerHelpers.UpdateParamServer(this.Params, this.ReceivedComfyNodes, this.InputNodeDic, this.OutputNodeDic);
            this.OnAttributesChanged();
            ExpireSolution(false);
        }

        private void ReflectOutputData(string nodeId, string imagePath)
        {
            // outputのParamにデータを反映させる
            Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
            {
                bool isExist = this.OutputNodeDic.TryGetValue(nodeId, out var param, out var node);
                if (!isExist) return;

                var data = imagePath;

                param.ClearData();
                param.AddVolatileData(new GH_Path(1), 0, data);

                param.Recipients[0].ExpireSolution(true);
            });
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");

        // 非同期処理を行うためのWorkerクラス
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
                ((ComfyGHComponent)Parent).InputNodeDic.ToList().ForEach(pair =>
                {
                    var id = pair.Key;
                    var nodeInfo = pair.Value;
                    var param = nodeInfo.Parameter;
                    object data = null;
                    DA.GetData(param.Name, ref data);
                    inputData.Add(id, new SendingData
                    {
                        Type = nodeInfo.Node.Type,
                        Data = data
                    });
                });
            }

            public override void SetData(IGH_DataAccess DA)
            {
                // Set image path to output params from outputImagesDic
                foreach (var output_image in ((ComfyGHComponent)Parent).outputImagesDic)
                {
                    var id = output_image.Key;
                    var imagePath = output_image.Value;
                    bool isExist = ((ComfyGHComponent)Parent).OutputNodeDic.TryGetValue(id, out var param, out var node);
                    if (!isExist) continue;
                    DA.SetData(param.Name, imagePath);
                }
            }

            public override async void DoWork(Action<string, double> ReportProgress, Action Done)
            {
                if (run)
                {
                    // initialize
                    ((ComfyGHComponent)Parent).outputImagesDic.Clear();

                    var serializeData = SerializeData(inputData);

                    Action<Dictionary<string, object>> OnProgress = (data) =>
                    {
                        var value = Convert.ToInt32(data["value"]);
                        var max = Convert.ToInt32(data["max"]);
                        ReportProgress(Id, (double)value / max);
                    };

                    Action<Dictionary<string, object>> OnExecuted = (data) =>
                    {
                        var imagePath = (string)data["image"];
                        var nodeId = (string)data["id"];
                        ((ComfyGHComponent)Parent).outputImagesDic[nodeId] = imagePath;
                        // データを受信したらoutputに逐次反映させる  
                        ((ComfyGHComponent)Parent).ReflectOutputData(nodeId, imagePath);
                    };

                    Action<Dictionary<string, object>> OnClose = (data) =>
                    {
                    };


                    try
                    {
                        await ConnectionHelper.QueuePrompt(serializeData, OnProgress, OnExecuted, OnClose);
                    }
                    catch (Exception e)
                    {
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                    }

                    Done();
                }


            }


            private Dictionary<string, SendingData> SerializeData(Dictionary<string, SendingData> data)
            {
                var serializeData = new Dictionary<string, SendingData>();
                foreach (var pair in data)
                {
                    string key = pair.Key;
                    SendingData value = pair.Value;
                    serializeData.Add(key, SerializeData(value));
                }
                return serializeData;
            }

            private SendingData SerializeData(SendingData data)
            {
                return new SendingData
                {
                    Type = data.Type,
                    Data = SerializeData(data.Data)
                };
            }

            private string SerializeData(object data)
            {
                if (data is GH_ComfyImage image)
                {
                    lock (ImagePreviewAttributes.bitmapLock)
                    {
                        using (MemoryStream stream = new MemoryStream())
                        {
                            image.Value.bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            byte[] bytes = stream.ToArray();
                            string base64Image = Convert.ToBase64String(bytes);
                            return base64Image;
                        }
                    }
                }
                else if (data is GH_String gH_String)
                {
                    return gH_String.Value;
                }

                return "";
            }

        }


    }
}