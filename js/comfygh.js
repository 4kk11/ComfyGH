import { app } from "../../scripts/app.js";
import { api } from "../../scripts/api.js";

app.registerExtension({
    name: "Comfy.ComfyGH",

    async setup(){
        api.addEventListener("queue_prompt", ({ detail }) => {
            app.queuePrompt(0);
        });
    },
    async beforeRegisterNodeDef(nodeType, nodeData, app) {
        if(nodeData.name === "LoadImageFromGH"){
            console.log(nodeData);
        }
    },
    loadedGraphNode(node, _) {
        if(node.type === "LoadImageFromGH"){
            console.log(node);
            api.addEventListener("update_preview", ({ detail }) => {
                const img = new Image();
                img.onload = () => {
                    node.imgs = [img];
                    app.graph.setDirtyCanvas(true);
                }
                
                const timestamp = new Date().getTime();

                img.src = `http://127.0.0.1:8188/view?filename=${detail.image}&type=input&subfolder=&timestamp=${timestamp}0`;

                node.setSizeForImage?.();
                // app.queuePrompt(0);
            });
        }
    },
})