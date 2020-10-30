using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSDLExporter
{
    class RadioButtonCollection
    {
        private UICheckBox[] cbs;

        public RadioButtonCollection(params UICheckBox[] cbs)
        {
            this.cbs = cbs;

            for(int i = 0; i < cbs.Length; i++)
            {
                cbs[i].eventCheckChanged += RadioButtonCollection_eventCheckChanged;
                cbs[i].isChecked = false;
            }

            cbs[0].isChecked = true; // should automatically uncheck all others
            cbs[0].readOnly = true;
        }

        private void RadioButtonCollection_eventCheckChanged(UIComponent component, bool value)
        {
            if (value)
            {
                foreach(UICheckBox cb in cbs)
                {
                    if((UICheckBox)component != cb)
                    {
                        cb.isChecked = false;

                        // allow enabling
                        cb.readOnly = false;
                    }
                    else
                    {
                        // forbid disabling
                        cb.readOnly = true;
                    }
                }
            }
        }
    }
}
