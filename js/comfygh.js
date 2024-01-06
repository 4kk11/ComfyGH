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

        api.addEventListener("status", ({detail}) => {
            const queue_remaining = detail.exec_info.queue_remaining;
            if(queue_remaining === 0){
                api.fetchApi('/custom_nodes/ComfyGH/close', {
                    method: 'POST',
                    body: JSON.stringify({ status: "done" })
                }).then(response => {}).catch(error => {
                    console.error('Error:', error);
                });
            }

        });

        api.addEventListener("get_workflow", ({detail}) => {

            const workflow = app.graph.serialize();
            let nodes = workflow.nodes.filter(node => node.type === "GH_LoadImage" || node.type === "GH_PreviewImage" || node.type === "GH_Text");
            nodes = nodes.map(node => {return {'id': node.id, 'type': node.type, 'nickname': node.title??(node.type + '_' + node.id)}});
            api.fetchApi('/custom_nodes/ComfyGH/send_workflow', {
                method: 'POST',
                body: JSON.stringify({ nodes })
            }).then(response => {}).catch(error => {
                console.error('Error:', error);
            });
        });

        api.addEventListener("update_gh_loadimage", ({detail}) => {
            const nodes = app.graph.findNodesByType("GH_LoadImage");
            for(const node of nodes) {
                const id = node.id;
                const object = detail[id];
                if(object === undefined) continue;

                const file_name = object.value;

                node.widgets[0].value = file_name;

                const img = new Image();
                img.onload = () => {
                    node.imgs = [img];
                    app.graph.setDirtyCanvas(true);
                }

                // add timestamp to prevent caching
                const timestamp = new Date().getTime();
                img.src = `http://127.0.0.1:8188/view?filename=${file_name}&type=input&subfolder=&timestamp=${timestamp}`;
                node.setSizeForImage?.();
            }
        });

        api.addEventListener("update_gh_text", ({detail}) => {
            const nodes = app.graph.findNodesByType("GH_Text");
            for(const node of nodes) {
                const id = node.id;
                const object = detail[id];
                if(object === undefined) continue;

                const text = object.value;
                node.widgets[0].value = text;
            }
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
    }
})