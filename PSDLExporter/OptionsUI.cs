using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    public class OptionsUI : UIPanel
    {
        public OptionsUI() { }

       /* public void OpenOptions()
        {
            UIView view = UIView.GetAView();

            UIPanel panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
            panel.name = "PSDL Export Options";

            UILabel title = panel.AddUIComponent<UILabel>();
            title.text = "Style:";

           /* UICheckBox styleLondon = panel.AddUIComponent<UICheckBox>();
            UILabel styleLondonlabel = styleLondon.AddUIComponent<UILabel>();
            styleLondonlabel.text = "London";
            styleLondon.isChecked = true;

            UICheckBox styleSF = panel.AddUIComponent<UICheckBox>();
            styleSF.label = new UILabel();
            styleSF.label.text = "San Francisco";
            styleSF.isChecked = false;

            UICheckBox styleCustom = panel.AddUIComponent<UICheckBox>();
            styleSF.label = new UILabel();
            styleCustom.label.text = "Custom:";
            styleCustom.isChecked = false;*//*

            panel.Show();
        }*/

        public override void Start()
        {
            base.Start();

            this.name = "PSDLExporterOptionsUI";
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(0, 0, 0, 255);
            this.width = 500;
            this.height = 500;

            UIHelper helper = new UIHelper(this);

            // road style
            UILabel l = this.AddUIComponent<UILabel>();
            l.text = "RoadStyle";
            l.eventClick += new MouseEventHandler((component, param) => { component.parent.Hide(); });
            l.relativePosition = new Vector3(10.0f, 10.0f, 0.0f);

            UICheckBox roadStyle_london = (UICheckBox)helper.AddCheckbox("London", true, (value) => { });
            roadStyle_london.relativePosition = new Vector3(10.0f, 35.0f, 0.0f);
            UICheckBox roadStyle_sf = (UICheckBox)helper.AddCheckbox("San Francisco", true, (value) => { });
            roadStyle_sf.relativePosition = new Vector3(250.0f, 35.0f, 0.0f);
            UICheckBox roadStyle_custom = (UICheckBox)helper.AddCheckbox("Custom: ", true, (value) => { });
            roadStyle_custom.relativePosition = new Vector3(10.0f, 55.0f, 0.0f);

            UITextField roadStyle_customName = this.AddUIComponent<UITextField>();
            roadStyle_customName.relativePosition = new Vector3(200.0f, 57.0f, 0.0f);
            roadStyle_customName.height = 16.0f;
            roadStyle_customName.width = 200.0f;
            roadStyle_customName.normalBgSprite = "TextFieldPanel";
            roadStyle_customName.hoveredBgSprite = "TextFieldPanelHovered";
            roadStyle_customName.builtinKeyNavigation = true;
            roadStyle_customName.readOnly = false;
            roadStyle_customName.canFocus = true;
            roadStyle_customName.isInteractive = true;
            roadStyle_customName.enabled = true;
            roadStyle_customName.textColor = Color.white;

            RadioButtonCollection roadStyle = new RadioButtonCollection(roadStyle_london, roadStyle_sf, roadStyle_custom);


            // confirmation buttons

            UIButton exportButton = this.AddUIComponent<UIButton>();
            exportButton.width = 200;
            exportButton.height = 40;
            exportButton.relativePosition = new Vector3(500 - 205, 500 - 45, 0.0f);
            exportButton.text = "Export";
            exportButton.eventClick += new MouseEventHandler(StartExport(roadStyle_london, roadStyle_sf, roadStyle_customName));
            SetupStandardSprites(ref exportButton);

            UIButton cancelButton = this.AddUIComponent<UIButton>();
            cancelButton.width = 200;
            cancelButton.height = 40;
            cancelButton.relativePosition = new Vector3(500 - 410, 500 - 45, 0.0f);
            cancelButton.text = "Cancel";
            cancelButton.eventClick += new MouseEventHandler((component, param) => { component.parent.Hide(); });
            SetupStandardSprites(ref cancelButton);
        }

        private static MouseEventHandler StartExport(UICheckBox roadStyle_london, UICheckBox roadStyle_sf, UITextField roadStyle_customName)
        {
            return (component, param) =>
            {
                // determine style
                string style;
                if (roadStyle_sf.isChecked)
                {
                    style = "f";
                }
                else if (roadStyle_london.isChecked)
                {
                    style = "l";
                }
                else
                {
                    if (RoadAnalyzer.IsCustomStyleValid(roadStyle_customName.text))
                    {
                        style = roadStyle_customName.text;
                    }
                    else
                    {
                        ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                        panel.SetMessage("PSDLExporter", "Style \"" + roadStyle_customName.text + "\" is not valid. Its name must only contain letters! Also note that \"l\" and \"f\"" +
                            "are reserved for London and San Francisco.", true);
                        return;
                    }
                }


                MeshExporter meshExporter = new MeshExporter();
                try
                {
                    style = style.ToLower();
                    meshExporter.RetrieveData(style);
                }
                catch (Exception ex)
                {
                    ExceptionPanel panel2 = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                    panel2.SetMessage("PSDLExporter", ex.Message + ex.StackTrace, true);
                }

                component.parent.Hide();

            };
        }

        private void SetupStandardSprites(ref UIButton button)
        {
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.focusedBgSprite = "ButtonMenuFocused";
            button.disabledBgSprite = "ButtonMenuDisabled";
        }
    }
}
