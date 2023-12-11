import torch
import folder_paths
import os
from PIL import Image, ImageOps
import numpy as np


class LoadImageFromGH:
    @classmethod
    def INPUT_TYPES(s):
        input_dir = folder_paths.get_input_directory()
        files = ["comfygh.png"]
        return {
            "required": {
                "image": (sorted(files), {"image_upload": False})
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



class SendImageToGH:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "raw_text": ("STRING", {"multiline": True})
            }
        }
    RETURN_TYPES = ()
    FUNCTION = "run"
    OUTPUT_NODE = True
    CATEGORY = "ComfyGH"

    def run(self, raw_text):
        return ()

NODE_CLASS_MAPPINGS = {
    'LoadImageFromGH': LoadImageFromGH,
    'SendImageToGH': SendImageToGH,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    'LoadImageFromGH': 'Load Image from grasshopper',
    'SendImageToGH': 'Send Image to grasshopper',
}
