using System;
using System.Diagnostics;
using System.IO;
using Rhino;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;
using Rhino.FileIO;
using Rhino.Geometry;

namespace ComfyGH
{
    public static class MeshLoader
    {
        public static Mesh FromBase64String(string base64string)
        {
            // decode base64 string to obj
            byte[] bytes = Convert.FromBase64String(base64string);
            string tempObjFilePath = Path.GetTempFileName();
            tempObjFilePath = Path.ChangeExtension(tempObjFilePath, ".obj");

            try
            {
                File.WriteAllBytes(tempObjFilePath, bytes);

                bool success = TryGetDocument(tempObjFilePath, true, out RhinoDoc doc);

                Mesh mesh = null;
                if (success && doc != null)
                {
                    ObjectTable otable = doc.Objects;
                    RhinoObject robj = otable.FindByObjectType(ObjectType.Mesh)[0];
                    mesh = (Mesh)robj.Geometry.Duplicate();
                    doc.Dispose();
                }

                if (mesh == null)
                    throw new Exception("Failed to load mesh");

                return mesh;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to load mesh from base64 string");
                throw e;
            }
            finally
            {
                if (File.Exists(tempObjFilePath))
                {
                    File.Delete(tempObjFilePath);
                }
            }
        }

        private static bool TryGetDocument(string filepath, bool import, out RhinoDoc document)
        {
            if (!File.Exists(filepath))
            {
                document = null;
                return false;
            }

            document = RhinoDoc.CreateHeadless(null);
            RhinoDoc activeDoc = RhinoDoc.ActiveDoc;

            if (activeDoc != null && !activeDoc.Equals(document))
            {
                document.ModelAbsoluteTolerance = activeDoc.ModelAbsoluteTolerance;
                document.ModelAngleToleranceDegrees = activeDoc.ModelAngleToleranceDegrees;
                document.ModelAngleToleranceRadians = activeDoc.ModelAngleToleranceRadians;
                document.ModelUnitSystem = activeDoc.ModelUnitSystem;
            }

            var readOptions = new Rhino.FileIO.FileReadOptions();
            readOptions.BatchMode = true;
            readOptions.ImportMode = true;
            FileObjReadOptions objOptions = new FileObjReadOptions(readOptions);
            objOptions.MapYtoZ = true;

            if (import && !FileObj.Read(filepath, document, objOptions))
            {
                document.Dispose();
                document = null;
                return false;
            }

            return true;
        }
    }



}
