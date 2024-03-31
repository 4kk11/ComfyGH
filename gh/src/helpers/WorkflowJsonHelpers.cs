using System;
using System.Linq;
using Newtonsoft.Json.Linq;


namespace ComfyGH
{
    public static class WorkflowJsonHelpers
    {
        public static void ApplyNextRandomSeed(JObject workflowJson)
        {
            // 各"widgets_values"を走査し、配列中に"randomize"がある場合、その一つ前の数値にランダムな数値を適用する

            foreach ( JObject node in workflowJson["nodes"].Cast<JObject>())
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
    }
}