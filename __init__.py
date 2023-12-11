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

@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/queue_prompt')
async def upload_file(request):
    data = await request.json()
    image_data = data.get('image')

    if image_data:
        image_data = base64.b64decode(image_data)
        input_dir = folder_paths.get_input_directory()
        file_path = os.path.join(input_dir, "comfygh.png")
        with open(file_path, "wb") as f:
            f.write(image_data)
        server.PromptServer.instance.send_sync("queue_prompt", { "status": "aaaaa" })
        return web.Response(text="ok")
    
    return web.Response(text="no image data", status=400)