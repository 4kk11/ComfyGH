import os
import shutil
import folder_paths
from .nodes import NODE_CLASS_MAPPINGS, NODE_DISPLAY_NAME_MAPPINGS
import nodes as comfy_nodes

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
    server.PromptServer.instance.send_sync("comfygh_progress", data, client_id)
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

@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/executed')
async def executed(request):
    data = await request.json()
    
    if(data.get('node_type') == "GH_SendImage"):
        saved_image_path = folder_paths.get_temp_directory()
        imageName = data.get('output_data')
        data['output_data'] = os.path.join(saved_image_path, imageName)

    
    server.PromptServer.instance.send_sync("comfygh_executed", data, client_id)
    return web.Response(text="ok")


@server.PromptServer.instance.routes.post('/custom_nodes/ComfyGH/test')
async def test(request):
    data = await request.json()
    print(f"Received JOSN: {data}")

    data = resolve_reroute(data)
    data = resolve_primitive(data)
    updated_data = transform_json(data)

    print(f"Updated JOSN: {updated_data}")

    return web.json_response(updated_data)


def transform_json(input_json):
    output_json = {}

    links = input_json["links"]
    nodes = input_json["nodes"]


    # nodes
    for node in nodes:
        node_id = node["id"]
        class_type = node["type"]
        
        if(class_type == "Reroute" or class_type == "PrimitiveNode"):
            continue

        node_info = comfy_nodes.NODE_CLASS_MAPPINGS[class_type]
        input_types = node_info.INPUT_TYPES()
        input_info = input_types["required"]

        if "optional" in input_types:
            input_info = {**input_info, **input_types["optional"]}

        inputs = {}
        if "inputs" in node:
            for input_item in node["inputs"]:

                # input_itemがNoneの場合はスキップ（PrimitiveNodeに繋がっている）
                if(input_item == None):
                    continue

                input_name = input_item["name"]

                # リンク情報(node_idとoutput_id)を格納
                link_id = input_item["link"]
                link = next((l for l in links if l[0] == link_id), None)
                if link:
                    inputs[input_name] = [str(link[1]), link[2]]
                del input_info[input_name]


        if "widgets_values" in node:
            widget_values = node["widgets_values"]
            i = 0
            for input_name in input_info:
                value = widget_values[i]
                inputs[input_name] = value
                i += 1
                if(input_name == "noise_seed" or input_name == "seed"):
                    i += 1


        
        output_json[node_id] = {
            "inputs": inputs,
            "class_type": class_type,
        }

    return output_json


def resolve_reroute(input_json):

    links = input_json["links"]
    
    
    for index, link in enumerate(links):
        if not link[5] == "*":
            continue

        reroute_id = link[3]

        # rerouteのinputに繋がっているノード
        source_node_id = link[1]
        source_node_param_id = link[2]

        # rerouteのoutputに繋がっているノードを探し、繋ぎ変える
        for _link in links:
            if not _link[1] == reroute_id:
                continue

            _link[1] = source_node_id
            _link[2] = source_node_param_id

    return input_json


def resolve_primitive(input_json):
    nodes = input_json["nodes"]
    links = input_json["links"]

    # PrimitiveNodeのIDを取得する
    primitive_node_ids = [node["id"] for node in nodes if node["type"] == "PrimitiveNode"]
    
    # PrimitiveNodeがsourceのlinkを探す
    for index, link in enumerate(links):
        if not (link[1] in primitive_node_ids):
            continue
    
        # linkのtargetのノードIDとparamIDを取得する
        target_node_id = link[3]
        target_param_id = link[4]

        for node in nodes:
            if not node["id"] == target_node_id:
                continue
            inputs = node["inputs"]
            # target_param_idに該当するinputをNoneにする
            inputs[target_param_id] = None


    return input_json
