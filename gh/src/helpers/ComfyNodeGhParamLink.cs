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
    // ComfyUIのノードとghコンポーネントのパラメータを紐づけるためのクラス
    public class ComfyNodeGhParamLink
    {
        public IGH_Param Parameter { get; }
        public ComfyNode Node { get; }

        public ComfyNodeGhParamLink(IGH_Param parameter, ComfyNode node)
        {
            Parameter = parameter;
            Node = node;
        }
    }


    // ComfyUIのノードidからComfyNodeGhParamLinkを取得できるようにするDicrionary
    public class ComfyNodeGhParamLookup : Dictionary<string, ComfyNodeGhParamLink>
    {
        public void Add(string key, IGH_Param param, ComfyNode node)
        {
            this[key] = new ComfyNodeGhParamLink(param, node);
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
            if (!this.ContainsKey(key)) return null;
            return this[key].Parameter;
        }

        public ComfyNode GetNode(string key)
        {
            if (!this.ContainsKey(key)) return null;
            return this[key].Node;
        }
    }
}