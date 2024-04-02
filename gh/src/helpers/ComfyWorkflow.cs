using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ComfyGH.Types;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino;

namespace ComfyGH
{
    public class ComfyWorkflow
    {
        private JObject _originalJsonObject;
        private JObject _editedJsonObject;

        public ComfyWorkflow(string _workflowJsonPath)
        {
            string workflowJsonPath = this.GetFullPath(_workflowJsonPath);
            if (!File.Exists(workflowJsonPath))
            {
                throw new FileNotFoundException("Workflow json file not found.");
            }

            string json = File.ReadAllText(workflowJsonPath);
            this._originalJsonObject = JObject.Parse(json);
            this._editedJsonObject = JObject.Parse(json);
        }

        private string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                // 絶対パスの場合はそのまま返す
                return path;
            }
            else
            {
                // 相対パスの場合は自身のライブラリのパスと結合して返す
                string libraryPath = Path.GetDirectoryName(typeof(ComfyWorkflow).Assembly.Location);
                string combinedPath = Path.Combine(libraryPath, path);
                string fullPath = Path.GetFullPath(combinedPath);
                Console.WriteLine(fullPath);
                return fullPath;
            }
        }

        public JObject GetOriginalJsonObject()
        {
            // return clone
            return JObject.Parse(this._originalJsonObject.ToString());
        }

        public JObject GetEditedJsonObject()
        {
            // return clone
            return JObject.Parse(this._editedJsonObject.ToString());
        }

        public void ApplyNextRandomSeed()
        {
            // 各"widgets_values"を走査し、配列中に"randomize"がある場合、その一つ前の数値にランダムな数値を適用する
            foreach (JObject node in this._editedJsonObject["nodes"].Cast<JObject>())
            {
                if (!node.ContainsKey("widgets_values")) continue;

                JArray widgetsValues = (JArray)node["widgets_values"];

                for (int i = 0; i < widgetsValues.Count; i++)
                {
                    if (widgetsValues[i].ToString() != "randomize") continue;
                    if (i > 0 && long.TryParse(widgetsValues[i - 1].ToString(), out long seed))
                    {
                        Random random = new Random();
                        long randomValue = random.Next();
                        widgetsValues[i - 1] = randomValue;
                    }
                }
            }
        }

        public void AddExtraProperty(string key, object value)
        {
            JObject extra = (JObject)this._editedJsonObject["extra"];
            extra[key] = JToken.FromObject(value);
        }

        public void AddNodeInputData(string nodeId, IGH_Goo data)
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
                    throw new ArgumentException("Unsupported data type.");
            }
            this.AddExtraProperty(nodeId, inputData);
        }

    }
}