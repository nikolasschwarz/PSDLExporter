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
            get { return "Exports roads as 3ds suitable for MM2CT new"; }
        }
    }

}
