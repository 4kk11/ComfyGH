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


namespace ComfyGH
{
    public static class GhParamServerHelpers
    {
       
        static public void UpdateParamServer(GH_ComponentParamServer parmServer, List<ComfyNode> comfyNodes, ComfyNodeGhParamLookup inputNodeDic, ComfyNodeGhParamLookup outputNodeDic)
        {
            GH_ComponentParamServer.IGH_SyncObject sync_data = parmServer.EmitSyncObject();

            // Unregist
            Dictionary<string, IEnumerable<IGH_Param>> idSourcesPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach (var id in inputNodeDic.Keys)
            {
                IGH_Param param = inputNodeDic.GetParam(id);
                idSourcesPairs.Add(id, param.Sources.ToList()); // copy sources list
                parmServer.UnregisterInputParameter(param);
                inputNodeDic.Remove(id);
            }

            Dictionary<string, IEnumerable<IGH_Param>> idRecipientsPairs = new Dictionary<string, IEnumerable<IGH_Param>>();
            foreach (var id in outputNodeDic.Keys)
            {
                IGH_Param param = outputNodeDic.GetParam(id);
                idRecipientsPairs.Add(id, param.Recipients.ToList());
                parmServer.UnregisterOutputParameter(param);
                outputNodeDic.Remove(id);
            }

            // Regist
            foreach (var node in comfyNodes)
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
                    case "GH_SendImage":
                        param = new Param_ComfyImage();
                        break;
                    case "GH_LoadText":
                        param = new Param_String();
                        isInput = true;
                        break;
                    case "GH_SendText":
                        param = new Param_String();
                        break;
                    case "GH_SendMesh":
                        param = new Param_Mesh();
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
                    inputNodeDic.Add(node.Id, param, node);
                    parmServer.RegisterInputParam(param);
                }
                else
                {
                    param.Access = GH_ParamAccess.item;
                    outputNodeDic.Add(node.Id, param, node);
                    parmServer.RegisterOutputParam(param);
                }
            }

            // Restoration sources
            foreach (var id in inputNodeDic.Keys)
            {
                var param = inputNodeDic.GetParam(id);
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
            foreach (var id in outputNodeDic.Keys)
            {
                var param = outputNodeDic.GetParam(id);
                if (idRecipientsPairs.ContainsKey(id))
                {
                    var recipients = idRecipientsPairs[id];
                    foreach (var recipient in recipients)
                    {
                        recipient.AddSource(param);
                    }
                }
            }


            parmServer.Sync(sync_data);
        }

        static public bool RegistInputDynamic<T>(GH_ComponentParamServer parmServer, string name, bool optional) where T: IGH_Param
        {
            if(IsExistInput(parmServer, name)) return false;

            GH_ComponentParamServer.IGH_SyncObject sync_data = parmServer.EmitSyncObject();

            IGH_Param param = (T)Activator.CreateInstance(typeof(T));
            param.Name = name;
            param.NickName = name;
            param.Access = GH_ParamAccess.item;
            param.Optional = optional;

            parmServer.RegisterInputParam(param);

            parmServer.Sync(sync_data);
            return true;
        }

        static public bool DeleteInputDynamic(GH_ComponentParamServer parmServer, string Name)
        {
            GH_ComponentParamServer.IGH_SyncObject sync_data = parmServer.EmitSyncObject();

            bool isDeleted = false;

            foreach (var input in parmServer.Input)
            {
                if (input.NickName == Name)
                {
                    parmServer.UnregisterInputParameter(input);
                    isDeleted = true;
                    break;
                }
            }

            parmServer.Sync(sync_data);
            return isDeleted;
        }

        static public bool IsExistInput(GH_ComponentParamServer parmServer, string intputName)
        {

            foreach (var input in parmServer.Input)
            {
                if (input.NickName == intputName)
                {
                    return true;
                }
            }

            return false;
        }

        
    }
}