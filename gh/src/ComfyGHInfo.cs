using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace ComfyGH
{
  public class ComfyGHInfo : GH_AssemblyInfo
  {
    public override string Name => "ComfyGH Info";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("58fbefd0-cc09-42d3-a0ff-9fe3b52e299b");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";
  }
}