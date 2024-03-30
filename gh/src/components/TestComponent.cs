using System;
using Grasshopper;
using Grasshopper.Kernel;
using ComfyGH.Attributes;

namespace ComfyGH.Components
{
    public class TestComponent : GH_Component
    {
        
        public TestComponent() : base("TestComponent", "Test", "Test component", "ComfyGH", "Test")
        {
        }
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Text to display", GH_ParamAccess.item);
            pManager.AddNumberParameter("Number", "N", "Number to display", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Visible", "V", "Visibility of the button", GH_ParamAccess.item, true);
            this.Params.Input[0].Optional = true;
            this.Params.Input[1].Optional = true;
            this.Params.Input[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Text", "T", "Text to display", GH_ParamAccess.item);
            
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool visibleButton = false;
            DA.GetData("Visible", ref visibleButton);

            (base.Attributes as ButtonAttributes).Visible = visibleButton;

            bool run = (base.Attributes as ButtonAttributes).Pressed;
            if(run)
                Console.WriteLine("Solving");
        }

        public override void CreateAttributes()
        {
            this.m_attributes = new ButtonAttributes(this);
        }

        public override Guid ComponentGuid => new Guid("52BA3072-1865-4BAD-BC00-23F3F2AE12DE");
    }
}