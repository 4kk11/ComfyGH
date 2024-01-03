import os
import shutil
import folder_paths
from .nodes import NODE_CLASS_MAPPINGS, NODE_DISPLAY_NAME_MAPPINGS

WEB_DIRECTORY = "./js"

__all__ = ['NODE_CLASS_MAPPINGS', 'NODE_DISPLAY_NAME_MAPPINGS', 'WEB_DIRECTORY']

import server
import json
import base64
import aiohttp
from aiohttp import web
import asyncio

client_id = "0CB33780A6EE4767A5DDC2AD41BFE975"

@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/queue_prompt')
async def upload_file(request):
    data = await request.json()

    # dataの中身は{id: string, ...}みたいな感じ
    # これを走査して、idからノードを特定→ノードに値を渡していく。もしかしたらtypeも必要かも
    # {id: {type: string, value: any}, ...} これがいいかも
    print(data)
    for id in data.keys():
        type = data[id]['type']
        value = data[id]['value']
        
        if(type == 'GH_Text'):
            server.PromptServer.instance.send_sync("update_text", {"node_id": id, "value": value})
        elif(type == "GH_LoadImage"):
            file_name = nodes.SOURCE_IMAGE_NAME
            image_data = base64.b64decode(value)
            input_dir = folder_paths.get_input_directory()
            file_path = os.path.join(input_dir, file_name)
            with open(file_path, "wb") as f:
                f.write(image_data)
            server.PromptServer.instance.send_sync("update_preview", { "node_id": id, "value": file_name })

        




    # dataから画像(base64string)を取得

    # dataからテキストを取得

    # image_data = data.get('image')
    # file_name = nodes.SOURCE_IMAGE_NAME

    # if image_data:
    #     image_data = base64.b64decode(image_data)
    #     input_dir = folder_paths.get_input_directory()
    #     file_path = os.path.join(input_dir, file_name)
    #     with open(file_path, "wb") as f:
    #         f.write(image_data)
    #     server.PromptServer.instance.send_sync("update_preview", { "image": file_name })

    #     server.PromptServer.instance.send_sync("queue_prompt", { })
    #     return web.Response(text="ok")
    
    return web.Response(text="no image data", status=400)


@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/progress')
async def propagate_progress(request):
    data = await request.json()
    value = data.get('value')
    max = data.get('max')
    server.PromptServer.instance.send_sync("comfygh_progress", { "value": value, "max": max}, client_id)
    return web.Response(text="ok")


@server.PromptServer.instance.routes.get('/custom_nodes/ComfyGH/get_workflow')
async def get_workflow(request):
    #data = request.json()
    server.PromptServer.instance.send_sync("get_workflow", { })
    return web.Response(text="ok")

@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/send_workflow')
async def send_workflow(request):
    data = await request.json()
    server.PromptServer.instance.send_sync("send_workflow", data, client_id)
    return web.Response(text="ok")