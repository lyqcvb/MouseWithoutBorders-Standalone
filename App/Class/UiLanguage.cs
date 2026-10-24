// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Forms;

namespace MouseWithoutBorders.Class
{
    internal static class UiLanguage
    {
        private static readonly Dictionary<string, string> Translations = LoadTranslations();
        private static readonly ConditionalWeakTable<object, Dictionary<string, string>> OriginalText = new();

        internal static string Language { get; private set; } = "zh-CN";

        internal static string Normalize(string language) =>
            string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";

        internal static void SetLanguage(string language)
        {
            Language = Normalize(language);
            // UI resources follow the selected language; number, network and date formats do not change.
            var culture = CultureInfo.GetCultureInfo(Language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        internal static string Text(string english)
        {
            if (Language == "en-US" || string.IsNullOrEmpty(english))
            {
                return english;
            }

            if (Translations.TryGetValue(english, out string translated)
                || Translations.TryGetValue(english.Trim(), out translated))
            {
                return translated;
            }

            foreach (string suffix in new[] { " [Managed]", " [Unsupported!]" })
            {
                if (english.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return Text(english[..^suffix.Length]) + Text(suffix);
                }
            }

            return english;
        }

        private static string Original(object owner, string property, string value)
        {
            var values = OriginalText.GetOrCreateValue(owner);
            if (!values.TryGetValue(property, out string original))
            {
                original = value;
                values.Add(property, original);
            }

            return original;
        }

        internal static void Apply(Control root, params ToolTip[] toolTips)
        {
            // Editable values are user data, even when they happen to match an English caption.
            if (!Equals(root.Tag, "UserData") && (root is Label or ButtonBase or GroupBox or TabPage
                || root is TextBox { ReadOnly: true, Name: "textBoxDNS" }))
            {
                root.Text = Text(Original(root, "Text", root.Text));
            }

            if (root is ImageButton)
            {
                root.Invalidate();
            }

            foreach (ToolTip toolTip in toolTips)
            {
                toolTip.SetToolTip(root, Text(Original(root, "Tip:" + toolTip.GetHashCode(), toolTip.GetToolTip(root))));
            }

            foreach (Control child in root.Controls)
            {
                if (child is Machine machine)
                {
                    machine.RefreshLanguage();
                }
                else
                {
                    Apply(child, toolTips);
                }
            }
        }

        internal static void Apply(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                // Runtime computer entries carry user-defined machine names.
                if (item.Tag is string tag && tag.StartsWith("MACHINE: ", StringComparison.Ordinal))
                {
                    continue;
                }

                item.Text = Text(Original(item, "Text", item.Text));
                item.ToolTipText = Text(Original(item, "Tip", item.ToolTipText));
                if (item is ToolStripMenuItem menu)
                {
                    Apply(menu.DropDownItems);
                }
            }
        }

        private static Dictionary<string, string> LoadTranslations()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var assembly = typeof(UiLanguage).Assembly;
            foreach (string name in assembly.GetManifestResourceNames().Where(name => name.EndsWith("Translations.json", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(name);
                foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(stream))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }
    }
}
