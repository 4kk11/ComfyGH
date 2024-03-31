import torch
import folder_paths
import hashlib
import os
import random
from PIL import Image, ImageOps
import numpy as np
import nodes
import server
import base64


class GH_LoadImage:
    @classmethod
    def INPUT_TYPES(s):
        input_dir = folder_paths.get_input_directory()
        files = [f for f in os.listdir(input_dir) if os.path.isfile(os.path.join(input_dir, f))]
        return {
            "required": {
                "image": (sorted(files), {"image_upload": True})
            }
        }
    RETURN_TYPES = ("IMAGE", )
    RETURN_NAMES = ("image", )
    FUNCTION = "run"

    CATEGORY = "ComfyGH"

    def run(self, image):
        image_path = folder_paths.get_annotated_filepath(image)
        i = Image.open(image_path)
        i = ImageOps.exif_transpose(i)
        image = i.convert("RGB")
        image = np.array(image).astype(np.float32) / 255.0
        image = torch.from_numpy(image)[None,]
        if 'A' in i.getbands():
            mask = np.array(i.getchannel('A')).astype(np.float32) / 255.0
            mask = 1. - torch.from_numpy(mask)
        else:
            mask = torch.zeros((64,64), dtype=torch.float32, device="cpu")
        return (image, mask.unsqueeze(0))
    

    @classmethod
    def IS_CHANGED(s, image):
        print("IS_CHANGED", image)
        image_path = folder_paths.get_annotated_filepath(image)
        m = hashlib.sha256()
        with open(image_path, 'rb') as f:
            m.update(f.read())
        return m.digest().hex()

    @classmethod
    def VALIDATE_INPUTS(s, image):
        if not folder_paths.exists_annotated_filepath(image):
            return "Invalid image file: {}".format(image)

        return True



class GH_SendImage(nodes.SaveImage):
    def __init__(self):
        self.output_dir = folder_paths.get_temp_directory()
        self.type = "temp"
        self.prefix_append = "_temp_" + ''.join(random.choice("abcdefghijklmnopqrstupvxyz") for x in range(5))
        self.compress_level = 4
    
    @classmethod
    def INPUT_TYPES(s):
        return {"required":
                    {"images": ("IMAGE", ), },
                "hidden": {"prompt": "PROMPT", "extra_pnginfo": "EXTRA_PNGINFO", "node_id": "UNIQUE_ID"},
                }
    
    RETURN_TYPES = ()
    FUNCTION = "run"
    OUTPUT_NODE = True
    CATEGORY = "ComfyGH"


    def run(self, images, filename_prefix = "ComfyUI", prompt = None, extra_pnginfo = None, node_id = None):

        result = super().save_images(images, filename_prefix, prompt, extra_pnginfo)

        filename = result["ui"]["images"][0]["filename"]
        image_path = os.path.join(folder_paths.get_temp_directory(), filename)
        with open(image_path, "rb") as f:
            encoded_string = base64.b64encode(f.read()).decode("utf-8")

        client_id = server.PromptServer.instance.client_id
        server.PromptServer.instance.send_sync("gh_send_image", {"image": encoded_string, "node_id": node_id}, client_id)

        return result
    
    
class GH_LoadText():
    @classmethod
    def INPUT_TYPES(s):
        return {"required": {"text": ("STRING", {"multiline": True})}}
    RETURN_TYPES = ("STRING", )
    FUNCTION = "run"
    CATEGORY = "ComfyGH"

    def run(self, text):
        return (text,)

class GH_SendText():
    @classmethod
    def INPUT_TYPES(s):
        return {"required": {"text": ("STRING", {"forceInput": True})}}
    OUTPUT_NODE = True
    RETURN_TYPES = ()
    FUNCTION  = "run"
    CATEGORY = "ComfyGH"

    def run(self, text):
        print(text)
        # いまはUIはついていないが、"executed"を発生させたいので、"ui"をつける
        return {"ui": {"container": [{"text": text}]}, "result": ()}


class GH_SendMesh():
    @classmethod
    def INPUT_TYPES(s):
        return {"required": {"obj_path": ("STRING", {"forceInput": True})},
                "hidden": {"prompt": "PROMPT", "node_id": "UNIQUE_ID"}}
    OUTPUT_NODE = True
    RETURN_TYPES = ()
    FUNCTION  = "run"
    CATEGORY = "ComfyGH"

    def run(self, obj_path, prompt = None, node_id = None):

        with open(obj_path, "rb") as obj_file:
            encoded_string = base64.b64encode(obj_file.read()).decode("utf-8")

        client_id = server.PromptServer.instance.client_id
        server.PromptServer.instance.send_sync("gh_send_mesh", {"mesh": encoded_string, "node_id": node_id}, client_id)

        return ()


NODE_CLASS_MAPPINGS = {
    'GH_LoadImage': GH_LoadImage,
    'GH_SendImage': GH_SendImage,
    'GH_LoadText': GH_LoadText,
    'GH_SendText': GH_SendText,
    'GH_SendMesh': GH_SendMesh,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    'GH_LoadImage': 'GH_LoadImage',
    'GH_SendImage': 'GH_SendImage',
    'GH_LoadText': 'GH_LoadText',
    'GH_SendText': 'GH_SendText',
    'GH_SendMesh': 'GH_SendMesh',
}
