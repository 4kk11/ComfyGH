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

    if data is None:
        return web.Response(text="no data", status=400)

    # data = {id: {type: string, value: any}, ...}

    try:
        for id in data.keys():
            type = data[id]['type']
            value = data[id]['value']
            
            if(type == "GH_LoadImage"):
                # value -> 画像(base64string)
                # 画像を保存して、valueをファイル名に変更
                file_name =  'comfygh_' + id + '.png'
                image_data = base64.b64decode(value)
                input_dir = folder_paths.get_input_directory()
                file_path = os.path.join(input_dir, file_name)
                with open(file_path, "wb") as f:
                    f.write(image_data)
                data[id]['value'] = file_name
    except Exception as e: 
        print(e)
        return web.Response(text="error", status=400)
        
    server.PromptServer.instance.send_sync("update_gh_loadimage", data)
    server.PromptServer.instance.send_sync("update_gh_text", data)
        
    server.PromptServer.instance.send_sync("queue_prompt", { })
    return web.Response(text="ok")
    

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


@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/close')
async def close(request):
    server.PromptServer.instance.send_sync("comfygh_close", { }, client_id)
    return web.Response(text="ok")