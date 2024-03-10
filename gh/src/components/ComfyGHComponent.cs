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
        private List<ComfyNode> nodes;

        protected override async void SolveInstance(IGH_DataAccess DA)
        {
            bool updateParams = false;
            DA.GetData(1, ref updateParams);
            if (updateParams)
            {
                try
                {
                    var nodes = await ConnectionHelper.GetGhNodesFromComfyUI();
                    this.nodes = nodes;
                    OnPingDocument().ScheduleSolution(1, SolutionCallback);
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

        private void SolutionCallback(GH_Document doc)
        {
            this.UpdateParamServer();
            ExpireSolution(false);
        }

        private ComfyNodeGhParamLookup InputNodeDic = new ComfyNodeGhParamLookup();
        private ComfyNodeGhParamLookup OutputNodeDic = new ComfyNodeGhParamLookup();

        private void UpdateParamServer()
        {
            GH_ComponentParamServer.IGH_SyncObject sync_data = base.Params.EmitSyncObject();

            // Unregist
            Dictionary<string, IEnumerable<IGH_Param>> idSourcesPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach (var id in InputNodeDic.Keys)
            {
                IGH_Param param = InputNodeDic.GetParam(id);
                idSourcesPairs.Add(id, param.Sources.ToList()); // copy sources list
                Params.UnregisterInputParameter(param);
                InputNodeDic.Remove(id);
            }

            Dictionary<string, IEnumerable<IGH_Param>> idRecipientsPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach (var id in OutputNodeDic.Keys)
            {
                IGH_Param param = OutputNodeDic.GetParam(id);
                idRecipientsPairs.Add(id, param.Recipients.ToList());
                Params.UnregisterOutputParameter(param);
                OutputNodeDic.Remove(id);
            }

            // Regist
            foreach (var node in this.nodes)
            {
                var nickname = node.Nickname;
                var type = node.Type;

                IGH_Param param;
                bool isInput = false;
                switch (type)
                {
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
                if (isInput)
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
            foreach (var id in InputNodeDic.Keys)
            {
                var param = InputNodeDic.GetParam(id);
                if (idSourcesPairs.ContainsKey(id))
                {
                    var sources = idSourcesPairs[id];
                    foreach (var source in sources)
                    {
                        param.AddSource(source);
                    }
                }
            }

            // Restoration recipients
            foreach (var id in OutputNodeDic.Keys)
            {
                var param = OutputNodeDic.GetParam(id);
                if (idRecipientsPairs.ContainsKey(id))
                {
                    var recipients = idRecipientsPairs[id];
                    foreach (var recipient in recipients)
                    {
                        recipient.AddSource(param);
                    }
                }
            }


            base.Params.Sync(sync_data);
            OnAttributesChanged();
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
                        // データを受信したらoutputを逐次更新する  
                        ((ComfyGHComponent)Parent).UpdateOutput(nodeId, imagePath);
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

        private void UpdateOutput(string nodeId, string imagePath)
        {
            Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
            {
                bool isExist = OutputNodeDic.TryGetValue(nodeId, out var param, out var node);
                if (!isExist) return;

                var data = imagePath;

                param.ClearData();
                param.AddVolatileData(new GH_Path(1), 0, data);

                param.Recipients[0].ExpireSolution(true);
            });
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");
    }
}