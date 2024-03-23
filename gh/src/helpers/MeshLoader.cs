using System;
using System.IO;
using Rhino;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;
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

            Console.WriteLine(tempObjFilePath);

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

                return mesh;
            }
            catch (Exception e)
            {
                Console.WriteLine(".objファイルの読み込みに失敗しました。");
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

            if (import && !document.Import(filepath))
            {
                document.Dispose();
                document = null;
                return false;
            }

            return true;
        }
    }



}
