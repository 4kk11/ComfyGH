import { app } from "../../scripts/app.js";
import { api } from "../../scripts/api.js";

app.registerExtension({
    name: "Comfy.ComfyGH",

    async setup(){
        api.addEventListener("queue_prompt", ({ detail }) => {
            app.queuePrompt(0);
        });
        
        api.addEventListener("progress", ({ detail }) => {
            const value = detail.value;
            const max = detail.max;
            
            api.fetchApi('/custom_nodes/ComfyGH/progress', {
                method: 'POST',
                body: JSON.stringify({ value, max })
            }).then(response => {}).catch(error => {
                console.error('Error:', error);
            });

        });

        api.addEventListener("get_workflow", ({detail}) => {

            const workflow = app.graph.serialize();
            let nodes = workflow.nodes.filter(node => node.type === "GH_LoadImage" || node.type === "GH_PreviewImage");
            nodes = nodes.map(node => {return {'id': node.id, 'type': node.type, 'nickname': node.title??(node.type + '_' + node.id)}});
            api.fetchApi('/custom_nodes/ComfyGH/send_workflow', {
                method: 'POST',
                body: JSON.stringify({ nodes })
            }).then(response => {}).catch(error => {
                console.error('Error:', error);
            });
        });

    },
    async beforeRegisterNodeDef(nodeType, nodeData, app) {
        if(nodeData.name === "GH_LoadImage"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
        if(nodeData.name === "GH_PreviewImage"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
        if(nodeData.name === "GH_Text"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
    },
    loadedGraphNode(node, _) {
        if(node.type === "GH_LoadImage"){
            // update node image preview
            api.addEventListener("update_preview", ({ detail }) => {
                const node_id = detail.id;
                const value = detail.value;
                console.log(node);

                if(node.id !== node_id) return;

                const img = new Image();
                img.onload = () => {
                    node.imgs = [img];
                    app.graph.setDirtyCanvas(true);
                }

                // add timestamp to prevent caching
                const timestamp = new Date().getTime();
                img.src = `http://127.0.0.1:8188/view?filename=${value}&type=input&subfolder=&timestamp=${timestamp}`;
                node.setSizeForImage?.();
            });
        }

        if(node.type === "GH_Text"){
            api.addEventListener("update_text", ({ detail }) => {
                const node_id = detail.node_id;
                const value = detail.value;

                if(node.id !== node_id) return;

                // どうやってノードにvalueを渡すか？    

            });
        }
    },
})