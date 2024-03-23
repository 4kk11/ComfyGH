using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace ComfyGH.Types
{
    public class GH_ComfyImage : GH_Goo<ComfyImage>
    {
        public GH_ComfyImage(){}

        public GH_ComfyImage(ComfyImage internal_data): base(internal_data)
        {
        }

        public GH_ComfyImage(GH_ComfyImage other): base(other)
        {
        }
        
        public override bool IsValid => Value != null;

        public override string TypeName => "CGH_Image";

        public override string TypeDescription => "";

        public override IGH_Goo Duplicate()
        {
            return new GH_ComfyImage(new ComfyImage(Value));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(Bitmap)))
            {
                target = (Q)(object)this.Value.bitmap.Clone();
                return true;
            }
            else if (typeof(Q).IsAssignableFrom(typeof(ComfyImage)))
            {
                target = (Q)(object)this.Value.Clone();
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source is Bitmap)
            {
                Value = new ComfyImage((Bitmap)source);
                return true;
            }
            else if (source is ComfyImage image)
            {
                Value = image;
                return true;
            }
            else if(source is GH_String)
            {   
                GH_String gh_string = (GH_String)source;
                try
                {
                    Value = new ComfyImage(gh_string.Value);
                }
                catch
                {
                    return false;
                }
                return true;
            }
            return false;
        }
        
    }
}
