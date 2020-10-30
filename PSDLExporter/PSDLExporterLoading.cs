using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PSDLExporter
{
    public class NetworkGrabberLoading : ILoadingExtension
    {

        private UIMultiStateButton m_button;
        private UISprite m_lockImage;

        public void OnCreated(ILoading loading)
        {
            
        }

        public void OnLevelLoaded(LoadMode mode)
        {
            if (mode == LoadMode.LoadAsset || mode == LoadMode.NewAsset) return;

            // Get the UIView object. This seems to be the top-level object for most
            // of the UI.
            var l_uiView = UIView.GetAView();

            // Add a new button to the view.
            m_button = (UIMultiStateButton)l_uiView.AddUIComponent(typeof(UIMultiStateButton));

            // Attach a sprite to the button
            m_lockImage = (UISprite)m_button.AddUIComponent(typeof(UISprite));

            // Set the text to show on the button tooltip.
            m_button.tooltip = "PSDLExporter - click to export";
            m_button.isTooltipLocalized = false;
            m_button.RefreshTooltip();
            m_button.spritePadding = new RectOffset();

            // Set the button dimensions.
            m_button.width = 36;
            m_button.height = 36;

            // Set the lock image
            m_lockImage.spriteName = "LockIcon";
            m_lockImage.position = new Vector3(18, -18);
            m_lockImage.width = 24;
            m_lockImage.height = 24;
            m_lockImage.Hide();

            if (m_lockImage.atlas == null || m_lockImage.atlas.material == null)
            {
               // DebugLog("Could not get reference material!!!");
                return;
            }

            // The sprite for the button can't be added to the InGame atlas, since the sprite data
            // seems to come from the atlas's texture, instead of the texture supplied by the SpriteData.
            // So a whole new atlas with the toggle button base images duplicated is needed.
            // Thanks to https://github.com/onewaycitystreets/StreetDirectionViewer/blob/master/ui/StreetDirectionViewerUI.cs

            String[] iconNames = {
                "RoadArrowIcon",
                "Base",
                "BaseFocused",
                "BaseHovered",
                "BasePressed",
                "BaseDisabled",
            };

            m_button.atlas = CreateTextureAtlas("PSDLExporter.PSDLExporterIcon.png", "PSDLExporter Atlas", m_lockImage.atlas.material, 36, 36, iconNames);

            // Background sprites

            // Disabled state
            UIMultiStateButton.SpriteSet backgroundSpriteSet0 = m_button.backgroundSprites[0];
            backgroundSpriteSet0.normal = "Base";
            backgroundSpriteSet0.disabled = "Base";
            backgroundSpriteSet0.hovered = "BaseHovered";
            backgroundSpriteSet0.pressed = "Base";
            backgroundSpriteSet0.focused = "Base";

            // Enabled state
            m_button.backgroundSprites.AddState();
            UIMultiStateButton.SpriteSet backgroundSpriteSet1 = m_button.backgroundSprites[1];
            backgroundSpriteSet1.normal = "BaseFocused";
            backgroundSpriteSet1.disabled = "BaseFocused";
            backgroundSpriteSet1.hovered = "BaseFocused";
            backgroundSpriteSet1.pressed = "BaseFocused";
            backgroundSpriteSet1.focused = "BaseFocused";

            // Forground sprites

            // Disabled state
            UIMultiStateButton.SpriteSet foregroundSpriteSet0 = m_button.foregroundSprites[0];
            foregroundSpriteSet0.normal = "RoadArrowIcon";
            foregroundSpriteSet0.disabled = "RoadArrowIcon";
            foregroundSpriteSet0.hovered = "RoadArrowIcon";
            foregroundSpriteSet0.pressed = "RoadArrowIcon";
            foregroundSpriteSet0.focused = "RoadArrowIcon";

            // Enabled state
            m_button.foregroundSprites.AddState();
            UIMultiStateButton.SpriteSet foregroundSpriteSet1 = m_button.foregroundSprites[1];
            foregroundSpriteSet1.normal = "RoadArrowIcon";
            foregroundSpriteSet1.disabled = "RoadArrowIcon";
            foregroundSpriteSet1.hovered = "RoadArrowIcon";
            foregroundSpriteSet1.pressed = "RoadArrowIcon";
            foregroundSpriteSet1.focused = "RoadArrowIcon";

            // Enable button sounds.
            m_button.playAudioEvents = true;

            // Place the button.
            m_button.transformPosition = new Vector3(-1.22f, 0.98f);

            // Respond to button click.
            m_button.eventMouseUp += ButtonMouseUp;

            // set up directory structure if it doesn't already exist.

            if(!Directory.Exists("PSDLExporter"))
            {
                Directory.CreateDirectory("PSDLExporter");
            }

            if (!Directory.Exists(@"PSDLExporter\Exports"))
            {
                Directory.CreateDirectory(@"PSDLExporter\Exports");
            }

            if (!Directory.Exists(@"PSDLExporter\CustomTextures"))
            {
                Directory.CreateDirectory(@"PSDLExporter\CustomTextures");
            }

        }

        public void OnLevelUnloading()
        {

        }

        public void OnReleased()
        {
            
        }

        // helper methods

        private static UITextureAtlas CreateTextureAtlas(string textureFile, string atlasName, Material baseMaterial, int spriteWidth, int spriteHeight, string[] spriteNames)
        {
            Texture2D texture = new Texture2D(spriteWidth * spriteNames.Length, spriteHeight, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;

            { // LoadTexture
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                Stream textureStream = assembly.GetManifestResourceStream(textureFile);

                if (textureStream == null)
                {
                    DebugLog("Failed loading image!!");
                    return null;
                }

                byte[] buf = new byte[textureStream.Length];  //declare arraysize
                textureStream.Read(buf, 0, buf.Length); // read from stream to byte array

                texture.LoadImage(buf);

                texture.Apply(true, true);
            }

            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();

            { // Setup atlas
                Material material = (Material)Material.Instantiate(baseMaterial);
                material.mainTexture = texture;

                atlas.material = material;
                atlas.name = atlasName;
            }

            // Add sprites
            for (int i = 0; i < spriteNames.Length; ++i)
            {
                float uw = 1.0f / spriteNames.Length;

                var spriteInfo = new UITextureAtlas.SpriteInfo()
                {
                    name = spriteNames[i],
                    texture = texture,
                    region = new Rect(i * uw, 0, uw, 1),
                };

                atlas.AddSprite(spriteInfo);
            }

            return atlas;
        }

        

        private void ButtonMouseUp(UIComponent p_component, UIMouseEventParameter p_eventParam)
        {
            

            //OptionsUI options = new OptionsUI();
            UIView v = UIView.GetAView();
            UIComponent uic = v.AddUIComponent(typeof(OptionsUI));
            uic.CenterToParent();

            uic.Show();

            
            //options.OpenOptions();

            // TODO: move to event handler

            
        }

        public static void DebugLog(String p_message)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "[PSDLExporter] " + p_message);
        }
    }
}
