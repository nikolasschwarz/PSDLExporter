using ICities;
using UnityEngine;

namespace PSDLExporter
{
    public class PSDLExporterMod : IUserMod
    {
        public string Name
        {
            get { return "PSDLExporter"; }
        }

        public string Description
        {
            get { return "Exports City as a PSDL File suitable for Midtown Madness 2"; }
        }
    }

}
