
class LoadImageFromGH:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "raw_text": ("STRING", {"multiline": True})
            }
        }
    RETURN_TYPES = ("STRING", )
    RETURN_NAMES = ("output", )
    FUNCTION = "run"

    CATEGORY = "ComfyGH"

    def run(self, raw_text):
        return (raw_text, )


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
