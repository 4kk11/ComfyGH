using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace ComfyGH
{
    public class ComfyWorkflow
    {
        private JObject _jsonObject;

        public ComfyWorkflow(string workflowJsonPath)
        {
            if (!File.Exists(workflowJsonPath))
            {
                throw new FileNotFoundException("Workflow json file not found.");
            }

            string json = File.ReadAllText(workflowJsonPath);
            this._jsonObject = JObject.Parse(json);
        }

        public void ApplyNextRandomSeed()
        {
            WorkflowJsonHelpers.ApplyNextRandomSeed(this._jsonObject);
        }

        public JObject GetJsonObject()
        {
            // return clone
            return JObject.Parse(this._jsonObject.ToString());
        }
    }
}