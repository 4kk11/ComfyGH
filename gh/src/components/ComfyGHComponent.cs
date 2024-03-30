using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using GrasshopperAsyncComponent;
using System.Linq;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Rhino.DocObjects.Tables;
using Grasshopper.Kernel.Parameters;


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
            pManager.AddTextParameter("URL", "URL", "", GH_ParamAccess.item);
            // pManager.AddTextParameter("Workflow", "Workflow", "", GH_ParamAccess.item);
            // pManager.AddBooleanParameter("Run", "Run", "", GH_ParamAccess.item, false);
            // pManager.AddBooleanParameter("UpdateParams", "UpdateParams", "", GH_ParamAccess.item, false);
            // this.Params.Input[1].Optional = true;
            // this.Params.Input[2].Optional = true;
            // this.Params.Input[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "", GH_ParamAccess.item);
        }

        // ComfyUIから送られてくる情報を格納するためにDictionary、keyはComfyUIのノードid
        Dictionary<string, object> outputObjectsDic = new Dictionary<string, object>();

        // ComfyUIのキャンバス上にあるGHノードを格納するList
        private List<ComfyNode> ReceivedComfyNodes;

        // ComfyUIのノードとコンポーネントのパラメータを紐づけた情報
        private ComfyNodeGhParamLookup InputNodeDic = new ComfyNodeGhParamLookup();
        private ComfyNodeGhParamLookup OutputNodeDic = new ComfyNodeGhParamLookup();

        private string URL;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            if(GhParamServerHelpers.IsExistInput(this.Params, "URL"))
            {
                int sourceCount = this.Params.Input[0].Sources.Count();
                if (sourceCount == 0)
                {
                    OnPingDocument().ScheduleSolution(1, DeleteInput_Workflow);
                }
            }    
            
            Console.WriteLine("BeforeSolveInstance");
        }

        protected override async void SolveInstance(IGH_DataAccess DA)
        {
            string url = "";
            DA.GetData("URL", ref url);
            this.URL = url;

            // 接続の検証（URL）
            bool isConnectionComfyGH = ConnectionHelper.ValidateComfyGHConnection(url);
            if (!isConnectionComfyGH)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to connect to ComfyGH. Please install ComfyGH on ComfyUI");
                OnPingDocument().ScheduleSolution(1, DeleteInput_Workflow);
                return;
            }

            // inputにworkflowのParamを追加する
            // すでに存在する場合はなにもしない
            // workflowのParamが存在しない -> 追加する
            // 接続の検証が失敗する || URLのParamが外れる -> 削除する
            OnPingDocument().ScheduleSolution(1, RegistInput_Workflow);
            
            string workflow = "";

            if(!GhParamServerHelpers.IsExistInput(this.Params, "Workflow")) return;

            DA.GetData("Workflow", ref workflow);

            if (workflow != "")
            {
                Console.WriteLine("Workflow: " + workflow);
            }
            return;
            
            bool updateParams = false;
            if(GhParamServerHelpers.IsExistInput(this.Params, "UpdateParams"))
                DA.GetData("UpdateParams", ref updateParams);
            
            // input/outputの取得（Workflow）
            if(updateParams)
            {
                try
                {
                    var nodes = await ConnectionHelper.GetGhNodes(url, workflow);
                    this.ReceivedComfyNodes = nodes;
                    OnPingDocument().ScheduleSolution(1, RegistParameters);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                    return;
                }

                return;
            }

            if(workflow != "")
            {
                try
                {
                    string test = await ConnectionHelper.TranslateWorkflow(url, workflow);
                    Console.WriteLine(test);
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                    return;
                }
                
            }


            // if (updateParams)
            // {
            //     // ComfyUIからノードを取得し、それを元にコンポーネントのinput/outputを更新する
            //     try
            //     {
            //         var nodes = await ConnectionHelper.GetGhNodesFromComfyUI(this.URL);
            //         this.ReceivedComfyNodes = nodes;
            //         OnPingDocument().ScheduleSolution(1, RegistParameters);
            //     }
            //     catch (Exception e)
            //     {
            //         AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
            //     }

            //     return;
            // }

            base.SolveInstance(DA);

            // 生成したデータをOutputに保持させておく
            // これをしないと、再計算の際(F5キーとか)にデータが消えてしまう
            foreach (var output_image in outputObjectsDic)
            {
                var id = output_image.Key;
                var imagePath = output_image.Value;
                bool isExist = OutputNodeDic.TryGetValue(id, out var param, out var node);
                if (!isExist) continue;
                DA.SetData(param.Name, imagePath);
            }
        }

        private void RegistParameters(GH_Document doc)
        {
            // ConfyUIから受け取ったノード情報を元に、コンポーネントのinput/outputを更新する
            GhParamServerHelpers.UpdateParamServer(this.Params, this.ReceivedComfyNodes, this.InputNodeDic, this.OutputNodeDic);
            this.OnAttributesChanged();
            ExpireSolution(false);
        }

        private void RegistInput_Workflow(GH_Document doc)
        {
            bool isRegisted = GhParamServerHelpers.RegistInputDynamic<Param_String>(this.Params, "Workflow", true);
            if (isRegisted)
            {
                this.OnAttributesChanged();
                ExpireSolution(false);
            }
        }

        private void DeleteInput_Workflow(GH_Document doc)
        {
            bool isDeleted = GhParamServerHelpers.DeleteInputDynamic(this.Params, "Workflow");
            if (isDeleted)
            {
                this.OnAttributesChanged();
                ExpireSolution(false);
            }
        }

        private void UpdateAttributes()
        {
            OnPingDocument().ScheduleSolution(1, (doc) => {
                this.OnAttributesChanged();
                ExpireSolution(false);
            });
        }

        private void ReflectOutputData(string nodeId, object outputData)
        {
            // outputのParamにデータを反映させる
            Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
            {
                bool isExist = this.OutputNodeDic.TryGetValue(nodeId, out var param, out var node);
                if (!isExist) return;

                var data = outputData;

                param.ClearData();
                param.AddVolatileData(new GH_Path(1), 0, data);

                if(param.Recipients.Count > 0)
                {
                    foreach (var recipient in param.Recipients)
                    {
                        recipient.ExpireSolution(true);
                    }
                }
            });
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");

        // 非同期処理を行うためのWorkerクラス
        private class ComfyWorker : WorkerInstance
        {
            bool run;

            Dictionary<string, SendingNodeInputData> inputData = new Dictionary<string, SendingNodeInputData>();
            public ComfyWorker(GH_Component _parent) : base(_parent)
            {
            }

            public override WorkerInstance Duplicate()
            {
                return new ComfyWorker(Parent);
            }

            public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
            {
                if(GhParamServerHelpers.IsExistInput(Params, "Run"))
                    DA.GetData("Run", ref run);

                // Get data from input node params
                ((ComfyGHComponent)Parent).InputNodeDic.ToList().ForEach(pair =>
                {
                    var id = pair.Key;
                    var link = pair.Value;
                    var param = link.Parameter;
                    var node = link.Node;

                    IGH_Goo data = null;
                    DA.GetData(param.Name, ref data);

                    try
                    {
                        SendingNodeInputData seindingData = SendingNodeInputData.Create(node.Type, data);
                        this.inputData.Add(id, seindingData);
                    }
                    catch (Exception e)
                    {
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, e.ToString());
                        this.inputData = null;
                    }
                });
            }

            public override void SetData(IGH_DataAccess DA)
            {
                // Set image path to output params from outputImagesDic
                // foreach (var output_object in ((ComfyGHComponent)Parent).outputObjectsDic)
                // {
                //     var id = output_object.Key;
                //     var value = output_object.Value;
                //     bool isExist = ((ComfyGHComponent)Parent).OutputNodeDic.TryGetValue(id, out var param, out var node);
                //     if (!isExist) continue;
                //     DA.SetData(param.Name, value);
                // }
            }

            public override async void DoWork(Action<string, double> ReportProgress, Action Done)
            {
                if (run && this.inputData != null)
                {
                    // initialize
                    ((ComfyGHComponent)Parent).outputObjectsDic.Clear();


                    Action<Dictionary<string, object>> OnProgress = (data) =>
                    {
                        var progressType = (string)data["progress_type"];
                        if(progressType == "number")
                        {
                            var value = Convert.ToInt32(data["value"]);
                            var max = Convert.ToInt32(data["max"]);
                            ReportProgress(Id, (double)value / max);
                        }
                        else if(progressType == "text")
                        {
                            var nodeType = (string)data["node_type"];
                            ((ComfyGHComponent)Parent).Message = String.Format("Executing {0}...", nodeType);
                            Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
                            {
                                ((ComfyGHComponent)Parent).OnDisplayExpired(true);
                            });
                        }
                        
                    };

                    Action<Dictionary<string, object>> OnReceivedImage = (data) =>
                    {
                        var nodeId = (string)data["node_id"];
                        string base64string = (string)data["image"];

                        ComfyImage image = ComfyImage.FromBase64String(base64string);

                        ((ComfyGHComponent)Parent).outputObjectsDic[nodeId] = image;
                        ((ComfyGHComponent)Parent).ReflectOutputData(nodeId, image);
                    };

                    Action<Dictionary<string, object>> OnReceivedMesh = (data) =>
                    {
                        var nodeId = (string)data["node_id"];
                        string base64string = (string)data["mesh"];
                        
                        Mesh mesh = MeshLoader.FromBase64String(base64string);

                        ((ComfyGHComponent)Parent).outputObjectsDic[nodeId] = mesh;
                        ((ComfyGHComponent)Parent).ReflectOutputData(nodeId, mesh);
                    };

                    Action<Dictionary<string, object>> OnClose = (data) =>
                    {
                    };


                    try
                    {
                        await ConnectionHelper.QueuePrompt(((ComfyGHComponent)Parent).URL, this.inputData, OnProgress, OnReceivedImage, OnReceivedMesh, OnClose);
                    }
                    catch (Exception e)
                    {
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                        return;
                    }

                    Done();
                }


            }


        }


    }
}