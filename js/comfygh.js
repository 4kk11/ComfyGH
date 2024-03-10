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
                body: JSON.stringify({ "progress_type": "number", "value": value, "max": max })
            }).then(response => {}).catch(error => {
                console.error('Error:', error);
            });

        });

        api.addEventListener("executing", ({detail}) => {
            const nodeId = detail;
            if(!nodeId) return;
            const node = app.graph.getNodeById(nodeId);
            const nodeType = node.title??node.type;
            console.log(node);
            api.fetchApi('/custom_nodes/ComfyGH/progress', {
                method: 'POST',
                body: JSON.stringify({"progress_type": "text", "node_type": nodeType})
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
            let nodes = workflow.nodes.filter(node => node.type === "GH_LoadImage" || node.type === "GH_SendImage" || node.type === "GH_LoadText" || node.type == "GH_SendText");
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
            const nodes = app.graph.findNodesByType("GH_LoadText");
            for(const node of nodes) {
                const id = node.id;
                const object = detail[id];
                if(object === undefined) continue;

                const text = object.value;
                node.widgets[0].value = text;
            }
        });

        api.addEventListener("executed", ({detail}) => {
            const nodeId = detail.node;
            const node = app.graph.getNodeById(nodeId);

            const fetch = (nodeType, nodeId, data) => {
                api.fetchApi('/custom_nodes/ComfyGH/executed', {
                    method: 'POST',
                    body: JSON.stringify({ "node_type": nodeType, "node_id": nodeId, "output_data": data })
                }).then(response => {}).catch(error => {
                    console.error('Error:', error);
                });
            }

            if(node.type == "GH_SendImage")
            {
                const imageName = detail.output.images[0].filename;
                fetch(node.type, nodeId, imageName);
            }
            else if(node.type == "GH_SendText")
            {
                const text = detail.output.container[0].text;
                fetch(node.type, nodeId, text);
            }
            
        });

    },
    async beforeRegisterNodeDef(nodeType, nodeData, app) {
        if(nodeData.name === "GH_LoadImage"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
        if(nodeData.name === "GH_SendImage"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
        if(nodeData.name === "GH_LoadText"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
        if(nodeData.name === "GH_SendText"){
            nodeType.prototype.color = LGraphCanvas.node_colors.green.color;
            nodeType.prototype.bgcolor = LGraphCanvas.node_colors.green.bgcolor;
        }
    }
})