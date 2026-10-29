using Liv.Lck.Settings;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Overview page showing Welcome, Version info, What's New, and helpful links.
    /// </summary>
    public class LckOverviewPage : LckSettingsPageBase
    {
        protected override bool IsOverviewPage => true;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LckOverviewPage(
                "Project/LCK/1 Overview",
                SettingsScope.Project,
                new HashSet<string>(new[] { "LCK", "LIV", "Overview", "Welcome", "Version" })
            );
            provider.label = "Overview";
            return provider;
        }

        public LckOverviewPage(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = CreateContentArea();
            var content = scrollView.Q<VisualElement>(className: "lck-content-area");

            // Hero section
            VisualElement hero = new VisualElement();
            hero.AddToClassList("lck-hero");

            // Logo
            Texture2D logo = GetLogoTexture();
            if (logo != null)
            {
                Image logoImage = new Image();
                logoImage.image = logo;
                logoImage.style.width = 100;
                logoImage.style.height = 54;
                logoImage.style.marginBottom = 4;
                logoImage.RegisterCallback<ClickEvent>(evt => Application.OpenURL("https://liv.tv"));
                logoImage.AddToClassList("lck-sidebar-logo");
                hero.Add(logoImage);
            }

            // Version info (under logo)
            Label versionLabel = new Label($"Version {LckSettings.Version}");
            versionLabel.style.fontSize = 10;
            versionLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            versionLabel.style.marginBottom = 16;
            hero.Add(versionLabel);

            Label heroTitle = new Label("Welcome to LIV Camera Kit");
            heroTitle.AddToClassList("lck-hero-title");
            hero.Add(heroTitle);

            Label heroDescription = new Label(
                "LIV Camera Kit (LCK) enables players to capture and share their VR experiences. " +
                "Use the Setup section to configure LCK for your project."
            );
            heroDescription.AddToClassList("lck-hero-description");
            hero.Add(heroDescription);

            // Quick action buttons
            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("lck-button-row");

            // Show "Complete Setup" button if game name is default or tracking ID is missing
            bool isGameNameDefault = true;
            bool isTrackingIdMissing = true;
            try
            {
                var settings = LckSettings.Instance;
                if (settings != null)
                {
                    isGameNameDefault = string.IsNullOrEmpty(settings.GameName) || settings.GameName == "MyGame";
                    isTrackingIdMissing = string.IsNullOrEmpty(settings.TrackingId);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckOverviewPage] Error reading settings: {ex.Message}");
            }

            if (isGameNameDefault || isTrackingIdMissing)
            {
                Button setupButton = CreateAccentButton("Complete Setup", () => SettingsService.OpenProjectSettings("Project/LCK/2 Setup"));
                buttonRow.Add(setupButton);
            }

            Button docsButton = CreatePrimaryButton("Documentation", () => Application.OpenURL("https://liv.mintlify.app/"));
            buttonRow.Add(docsButton);

            // Discord button with icon
            Button discordButton = new Button(() => Application.OpenURL("https://discord.gg/liv"));
            discordButton.AddToClassList("lck-button");
            discordButton.AddToClassList("lck-button--secondary");
            discordButton.AddToClassList("lck-button-with-icon");

            // Load Discord icon (hidden on Unity 2020.3 due to sizing issues)
#if UNITY_2021_2_OR_NEWER
            var discordIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/gg.obi.unity.qck/Editor/SetupWizard/Icons/Discord-Symbol-White.png");
            if (discordIcon == null)
            {
                discordIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/tv.liv.lck/Editor/SetupWizard/Icons/Discord-Symbol-White.png");
            }

            if (discordIcon != null)
            {
                Image iconImage = new Image();
                iconImage.image = discordIcon;
                iconImage.AddToClassList("lck-button-icon");
                iconImage.AddToClassList("lck-button-icon--discord");
                discordButton.Add(iconImage);
            }
#endif

            Label discordLabel = new Label("Discord Community");
            discordButton.Add(discordLabel);

            buttonRow.Add(discordButton);

            hero.Add(buttonRow);
            content.Add(hero);

            // What's New section
            DrawWhatsNew(content);

            // Quick Links section
            AddSectionHeader(content, "Quick Links");

            var linksCard = CreateCard();

            AddLinkRow(linksCard, "Setup Guide", "Get started with LCK integration", () =>
                SettingsService.OpenProjectSettings("Project/LCK/2 Setup"));

            AddLinkRow(linksCard, "Configuration", "Adjust recording, audio, and advanced settings", () =>
                SettingsService.OpenProjectSettings("Project/LCK/3 Configuration"));

            AddLinkRow(linksCard, "Project Validation", "Check your project setup", () =>
                SettingsService.OpenProjectSettings("Project/LCK/4 Validation"));

            AddLinkRow(linksCard, "API Documentation", "Learn the LCK API", () =>
                Application.OpenURL("https://liv.mintlify.app/api-reference/unity/introduction"), isExternal: true);

            content.Add(linksCard);

            root.Add(scrollView);
        }

        private void DrawWhatsNew(VisualElement parent)
        {
            var changelogEntries = ParseChangelog(1);
            if (changelogEntries.Count > 0)
            {
                var latestEntry = changelogEntries[0];

                AddSectionHeader(parent, $"What's New in {latestEntry.Version}");

                VisualElement versionCard = CreateCard();

                if (latestEntry.Added.Count > 0)
                {
                    AddChangelogSection(versionCard, "Added", latestEntry.Added, "#D4FF2A", "d_Toolbar Plus");
                }

                if (latestEntry.Changed.Count > 0)
                {
                    AddChangelogSection(versionCard, "Changed", latestEntry.Changed, "#FFB84D", "d_refresh");
                }

                if (latestEntry.Fixed.Count > 0)
                {
                    AddChangelogSection(versionCard, "Fixed", latestEntry.Fixed, "#42A5F5", "d_FilterSelectedOnly");
                }

                // Full changelog link
                AddSpacer(versionCard, "sm");
                Button fullChangelogButton = new Button(() => Application.OpenURL("https://docs.liv.tv/unity/changelog"));
                fullChangelogButton.text = "View Full Changelog";
                fullChangelogButton.AddToClassList("lck-button");
                fullChangelogButton.AddToClassList("lck-button--small");
                fullChangelogButton.AddToClassList("lck-button--secondary");
                versionCard.Add(fullChangelogButton);

                parent.Add(versionCard);
            }
        }

        private void AddChangelogSection(VisualElement parent, string title, List<string> items, string colorHex, string iconName)
        {
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 12;
            header.style.marginBottom = 8;

            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                ColorUtility.TryParseHtmlString(colorHex, out Color iconColor);

                Image icon = new Image();
                icon.image = iconContent.image;
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 8;
                icon.tintColor = iconColor;
                header.Add(icon);
            }

            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            header.Add(titleLabel);

            parent.Add(header);

            foreach (var item in items)
            {
                VisualElement itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.FlexStart;
                itemRow.style.marginBottom = 4;
                itemRow.style.marginLeft = 24;

                Label bullet = new Label("\u2022");
                bullet.style.color = new Color(0.63f, 0.63f, 0.63f);
                bullet.style.marginRight = 8;
                bullet.style.minWidth = 10;
                itemRow.Add(bullet);

                Label itemLabel = new Label(item);
                itemLabel.AddToClassList("lck-body-text");
                itemLabel.style.marginBottom = 0;
                itemLabel.style.flexShrink = 1;
                itemLabel.style.whiteSpace = WhiteSpace.Normal;
                itemRow.Add(itemLabel);

                parent.Add(itemRow);
            }
        }

        private void AddLinkRow(VisualElement parent, string title, string description, Action onClick, bool isExternal = false)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.16f, 0.16f, 0.18f);

            VisualElement textContainer = new VisualElement();

            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.color = Color.white;
            titleLabel.style.marginBottom = 2;
            textContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-body-text");
            descLabel.style.marginBottom = 0;
            textContainer.Add(descLabel);

            row.Add(textContainer);

            Button goButton = new Button(onClick);
            goButton.text = "Open";
            goButton.AddToClassList("lck-button");
            goButton.AddToClassList("lck-button--small");
            goButton.AddToClassList(isExternal ? "lck-button--primary" : "lck-button--accent");
            row.Add(goButton);

            parent.Add(row);
        }

        #region Changelog Parsing

        private class ChangelogEntry
        {
            public string Version;
            public string Date;
            public List<string> Added = new List<string>();
            public List<string> Changed = new List<string>();
            public List<string> Fixed = new List<string>();
        }

        private List<ChangelogEntry> ParseChangelog(int maxEntries = 1)
        {
            List<ChangelogEntry> entries = new List<ChangelogEntry>();

            try
            {
                var changelogAsset = GetChangelogAsset();
                if (changelogAsset == null)
                {
                    return entries;
                }

                string content = changelogAsset.text;
                if (string.IsNullOrEmpty(content))
                {
                    return entries;
                }

                string[] lines = content.Split('\n');

                ChangelogEntry currentEntry = null;
                string currentSection = null;

                Regex versionRegex = new Regex(@"##\s*\[(\d+\.\d+\.\d+)\]\s*-?\s*(.*)");

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();

                    var versionMatch = versionRegex.Match(line);
                    if (versionMatch.Success)
                    {
                        // Add previous entry if it has content
                        if (currentEntry != null && HasContent(currentEntry))
                        {
                            entries.Add(currentEntry);
                            if (entries.Count >= maxEntries) break;
                        }

                        currentEntry = new ChangelogEntry
                        {
                            Version = versionMatch.Groups[1].Value,
                            Date = versionMatch.Groups[2].Value.Trim()
                        };
                        currentSection = null;
                        continue;
                    }

                    if (currentEntry == null) continue;

                    if (line.StartsWith("### Added"))
                    {
                        currentSection = "Added";
                    }
                    else if (line.StartsWith("### Changed"))
                    {
                        currentSection = "Changed";
                    }
                    else if (line.StartsWith("### Fixed"))
                    {
                        currentSection = "Fixed";
                    }
                    else if (line.StartsWith("- ") && currentSection != null)
                    {
                        string item = line.Substring(2).Trim();
                        if (!string.IsNullOrEmpty(item))
                        {
                            switch (currentSection)
                            {
                                case "Added": currentEntry.Added.Add(item); break;
                                case "Changed": currentEntry.Changed.Add(item); break;
                                case "Fixed": currentEntry.Fixed.Add(item); break;
                            }
                        }
                    }
                }

                // Add the last entry if it has content
                if (currentEntry != null && entries.Count < maxEntries && HasContent(currentEntry))
                {
                    entries.Add(currentEntry);
                }
            }
            catch (Exception)
            {
                // Silently handle parsing errors
            }

            return entries;
        }

        private bool HasContent(ChangelogEntry entry)
        {
            return entry.Added.Count > 0 || entry.Changed.Count > 0 || entry.Fixed.Count > 0;
        }

        #endregion
    }
}
