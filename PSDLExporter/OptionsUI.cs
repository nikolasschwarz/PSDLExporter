using ColossalFramework;
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
            //l.eventClick += new MouseEventHandler((component, param) => { component.parent.Hide(); });
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
            roadStyle_customName.textColor = Color.black;

            RadioButtonCollection roadStyle = new RadioButtonCollection(roadStyle_london, roadStyle_sf, roadStyle_custom);

            // file name
            UILabel filenameLabel = this.AddUIComponent<UILabel>();
            filenameLabel.text = "Filename: ";
            filenameLabel.relativePosition = new Vector3(10.0f, 500 - 65, 0.0f);

            UITextField filenameTextField = this.AddUIComponent<UITextField>();
            filenameTextField.relativePosition = new Vector3(100.0f, 500 - 65, 0.0f);
            filenameTextField.height = 16.0f;
            filenameTextField.width = 390.0f;
            filenameTextField.normalBgSprite = "TextFieldPanel";
            filenameTextField.hoveredBgSprite = "TextFieldPanelHovered";
            filenameTextField.builtinKeyNavigation = true;
            filenameTextField.readOnly = false;
            filenameTextField.canFocus = true;
            filenameTextField.isInteractive = true;
            filenameTextField.enabled = true;
            filenameTextField.textColor = Color.black;
            string name = "";
            if (Singleton<SimulationManager>.instance.m_metaData.m_CityName != null)
            {
                name += Singleton<SimulationManager>.instance.m_metaData.m_CityName;
            }
            filenameTextField.text = name;


            // confirmation buttons

            UIButton exportButton = this.AddUIComponent<UIButton>();
            exportButton.width = 200;
            exportButton.height = 40;
            exportButton.relativePosition = new Vector3(500 - 205, 500 - 45, 0.0f);
            exportButton.text = "Export";
            exportButton.eventClick += new MouseEventHandler(StartExport(roadStyle_london, roadStyle_sf, roadStyle_customName, filenameTextField));
            SetupStandardSprites(ref exportButton);

            UIButton cancelButton = this.AddUIComponent<UIButton>();
            cancelButton.width = 200;
            cancelButton.height = 40;
            cancelButton.relativePosition = new Vector3(500 - 410, 500 - 45, 0.0f);
            cancelButton.text = "Cancel";
            cancelButton.eventClick += new MouseEventHandler((component, param) => { component.parent.Hide(); });
            SetupStandardSprites(ref cancelButton);
        }

        private static MouseEventHandler StartExport(UICheckBox roadStyle_london, UICheckBox roadStyle_sf, UITextField roadStyle_customName, UITextField filename)
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
                    meshExporter.RetrieveData(filename.text, style);
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
