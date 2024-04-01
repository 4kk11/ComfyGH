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
using ComfyGH.Attributes;
using Newtonsoft.Json.Linq;


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
        private ComfyWorkflow Workflow;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // URLの接続がない場合は、SolveInstanceが実行されないので、ここでInputの削除を行う
            int sourceCount = GhParamServerHelpers.GetInputDataCount(this.Params, "URL");
            if (sourceCount == 0)
            {
                this.Reset(true, true);
            }

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Inputの検証（URL）
            int sourceCount = GhParamServerHelpers.GetInputDataCount(this.Params, "URL");
            if (sourceCount > 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "URL should be connected only one");
                this.Reset(true, true);
                return;
            }

            string url = "";
            DA.GetData("URL", ref url);
            this.URL = url;

            // 接続の検証（URL）
            bool isConnectionComfyGH = ConnectionHelper.ValidateComfyGHConnection(url);
            if (!isConnectionComfyGH)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to connect to ComfyGH. Please install ComfyGH on ComfyUI");
                this.Reset(true, true);
                return;
            }

            // inputにworkflowのParamを追加する
            // すでに存在する場合はなにもしない
            OnPingDocument().ScheduleSolution(1, RegistInput_Workflow);

            if (!GhParamServerHelpers.IsExistInput(this.Params, "Workflow")) return;

            string workflow = "";
            DA.GetData("Workflow", ref workflow);
            
            // Inputの検証（Workflow)      
            if (workflow == "")
            {
                this.Reset(false, true);
                return;
            }

            // Workflowの検証
            try
            {
                this.Workflow = new ComfyWorkflow(workflow);
                var nodes = ConnectionHelper.GetGhNodes(url, workflow);
                this.ReceivedComfyNodes = nodes;
                OnPingDocument().ScheduleSolution(1, UpdateComfyParameters);
                SetVisibleButton(true);
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                this.Reset(false, true);
                return;
            }

            // 非同期処理
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

        private void Reset(bool removeWorkflowParam, bool removeComfyParams)
        {
            if (removeWorkflowParam)
            {
                OnPingDocument().ScheduleSolution(1, DeleteInput_Workflow);
            }

            if (removeComfyParams)
            {
                OnPingDocument().ScheduleSolution(1, RemoveComfyParameters);
                SetVisibleButton(false);
            }

            this.Message = "";
            SetRunningState(RunningState.Idle);
        }

        private void UpdateComfyParameters(GH_Document doc)
        {
            // ConfyUIから受け取ったノード情報を元に、コンポーネントのinput/outputを更新する
            bool isUpdated = GhParamServerHelpers.UpdateComfyParams(this.Params, this.ReceivedComfyNodes, this.InputNodeDic, this.OutputNodeDic);
            if (isUpdated)
            {
                this.OnAttributesChanged();
                ExpireSolution(false);
            }
        }

        private void RemoveComfyParameters(GH_Document doc)
        {
            bool isRemoved = GhParamServerHelpers.RemoveComfyParams(this.Params, this.InputNodeDic, this.OutputNodeDic);
            if (isRemoved)
            {
                this.OnAttributesChanged();
                ExpireSolution(false);
            }
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

                if (param.Recipients.Count > 0)
                {
                    foreach (var recipient in param.Recipients)
                    {
                        recipient.ExpireSolution(true);
                    }
                }
            });
        }

        private void SetVisibleButton(bool visible)
        {
            (base.Attributes as ButtonAttributes).Visible = visible;
        }

        private void SetEnabledButton(bool enabled)
        {
            (base.Attributes as ButtonAttributes).Enabled = enabled;
        }

        private void SetRunningState(RunningState state)
        {
            (base.Attributes as ButtonAttributes).RunningState = state;
        }

        public override void CreateAttributes()
        {
            this.m_attributes = new ButtonAttributes(this);
            SetVisibleButton(false);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("064ee850-b34f-416e-b497-5929f70c33c1");

        // 非同期処理を行うためのWorkerクラス
        private class ComfyWorker : WorkerInstance
        {
            bool run;
            public ComfyWorker(GH_Component _parent) : base(_parent)
            {
            }

            public override WorkerInstance Duplicate()
            {
                return new ComfyWorker(Parent);
            }

            public override void GetData(IGH_DataAccess DA, GH_ComponentParamServer Params)
            {
                this.run = (Parent.Attributes as ButtonAttributes).Pressed;

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
                        ((ComfyGHComponent)Parent).Workflow.AddNodeInputData(id, data);
                    }
                    catch (Exception e)
                    {
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, e.ToString());
                    }
                });

                // random seedを更新
                ((ComfyGHComponent)Parent).Workflow.ApplyNextRandomSeed();
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
                // if (run && this.inputData != null)
                if (run)
                {
                    // initialize
                    ((ComfyGHComponent)Parent).outputObjectsDic.Clear();

                    // QueuePrompt時のActionを定義
                    int myQueuePosition = int.MaxValue;
                    int prevQueueRemaining = 0;
                    Action<Dictionary<string, object>> OnStatus = (data) =>
                    {
                        var status = (JObject)data["status"];
                        var execInfo = (JObject)status["exec_info"];
                        var currQueueRemaining = Convert.ToInt32(execInfo["queue_remaining"]);

                        // キューが進んだ場合、自分のキュー位置を更新
                        if (currQueueRemaining < prevQueueRemaining)
                        {
                            myQueuePosition -= prevQueueRemaining - currQueueRemaining;
                        }

                        // 自分が最後尾にいる場合（キューに追加された時）、キューの位置を登録
                        if (currQueueRemaining < myQueuePosition)
                        {
                            myQueuePosition = currQueueRemaining;
                        }

                        prevQueueRemaining = currQueueRemaining;

                        ((ComfyGHComponent)Parent).Message = String.Format("Waiting in queue... {0} remaining", myQueuePosition);
                        Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
                        {
                            ((ComfyGHComponent)Parent).OnDisplayExpired(true);
                        });
                    };

                    Action<Dictionary<string, object>> OnProgress = (data) =>
                    {
                        var value = Convert.ToInt32(data["value"]);
                        var max = Convert.ToInt32(data["max"]);
                        ReportProgress(Id, (double)value / max);
                    };

                    Action<Dictionary<string, object>> OnExecuting = (data) =>
                    {
                        var nodeId = (string)data["node"];
                        ((ComfyGHComponent)Parent).Message = String.Format("Executing {0}...", nodeId);
                        Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
                        {
                            ((ComfyGHComponent)Parent).OnDisplayExpired(true);
                        });
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


                    // QueuePromptを実行
                    try
                    {
                        ((ComfyGHComponent)Parent).SetEnabledButton(false);
                        ((ComfyGHComponent)Parent).SetRunningState(RunningState.Running);
                        await ConnectionHelper.QueuePrompt(((ComfyGHComponent)Parent).URL, ((ComfyGHComponent)Parent).Workflow, OnStatus, OnProgress, OnExecuting, OnReceivedImage, OnReceivedMesh);
                    }
                    catch (Exception e)
                    {
                        Parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.ToString());
                        return;
                    }
                    finally
                    {
                        ((ComfyGHComponent)Parent).SetEnabledButton(true);
                    }

                    ((ComfyGHComponent)Parent).SetRunningState(RunningState.Finished);
                    Done();
                }


            }


        }


    }
}