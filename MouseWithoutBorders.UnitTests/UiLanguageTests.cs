// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class UiLanguageTests
{
    [TestMethod]
    public void ExistingSettingsWithoutLanguage_DefaultToChinese()
    {
        var settings = JsonSerializer.Deserialize<MouseWithoutBordersSettings>("{\"properties\":{\"ShareClipboard\":{\"value\":false}}}");
        Assert.IsNotNull(settings);
        Assert.AreEqual("zh-CN", settings.Properties.Language);
        Assert.IsFalse(settings.Properties.ShareClipboard);
    }

    [TestMethod]
    public void EnglishPreference_RoundTripsThroughSettings()
    {
        var original = new MouseWithoutBordersSettings();
        var settings = new MouseWithoutBorders.Class.Settings(new SettingsUtils(new FileSystem()), original, new PassthroughPairingKeyCodec())
        {
            PauseInstantSaving = true,
        };
        settings.Language = "en-US";
        var loaded = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(JsonSerializer.Serialize(original));
        Assert.IsNotNull(loaded);
        Assert.AreEqual("en-US", loaded.Properties.Language);
        settings.Language = "unsupported";
        Assert.AreEqual("zh-CN", settings.Language);
    }

    [TestMethod]
    public void SwitchingLanguage_RestoresCaptionsAndTips_WithoutTranslatingUserData()
    {
        OnStaThread(() =>
        {
            using var root = new Panel();
            using var label = new Label { Text = "&Share Clipboard [Managed]" };
            using var machineName = new TextBox { Text = "Connected" };
            using var mapping = new TextBox { Name = "textBoxMachineName2IPPolicyList", ReadOnly = true, Text = "All" };
            using var tip = new ToolTip();
            root.Controls.AddRange(new Control[] { label, machineName, mapping });
            tip.SetToolTip(label, "Save changes and reconnect to the machines in the matrix.");
            UiLanguage.SetLanguage("zh-CN");
            UiLanguage.Apply(root, tip);
            Assert.AreEqual("共享剪贴板(&S) [由策略管理]", label.Text);
            Assert.AreEqual("保存更改，并重新连接布局中的电脑。", tip.GetToolTip(label));
            Assert.AreEqual("Connected", machineName.Text);
            Assert.AreEqual("All", mapping.Text);
            UiLanguage.SetLanguage("en-US");
            UiLanguage.Apply(root, tip);
            Assert.AreEqual("&Share Clipboard [Managed]", label.Text);
            Assert.AreEqual("Save changes and reconnect to the machines in the matrix.", tip.GetToolTip(label));
            UiLanguage.SetLanguage("zh-CN");
            UiLanguage.Apply(root, tip);
            Assert.AreEqual("共享剪贴板(&S) [由策略管理]", label.Text);
        });
    }

    [TestMethod]
    public void TrayTranslation_PreservesMachineNamesAndMenuIdentity()
    {
        OnStaThread(() =>
        {
            using var menu = new ContextMenuStrip();
            var all = new ToolStripMenuItem("All");
            var machine = new ToolStripMenuItem("All") { Tag = "MACHINE: All" };
            menu.Items.AddRange(new ToolStripItem[] { all, machine });
            UiLanguage.SetLanguage("zh-CN");
            UiLanguage.Apply(menu.Items);
            Assert.AreSame(all, menu.Items[0]);
            Assert.AreEqual("所有电脑", all.Text);
            Assert.AreEqual("All", machine.Text);
            UiLanguage.SetLanguage("en-US");
            UiLanguage.Apply(menu.Items);
            Assert.AreEqual("All", all.Text);
        });
    }

    [TestMethod]
    public void SettingsWindow_LanguageSelectorPreservesUnsavedInputAndEasyMouseMode()
    {
        OnStaThread(() =>
        {
            var previous = Setting.Values;
            var model = new MouseWithoutBordersSettings();
            model.Properties.EasyMouse.Value = (int)EasyMouseOption.Shift;
            Setting.Values = new MouseWithoutBorders.Class.Settings(new SettingsUtils(new FileSystem()), model, new PassthroughPairingKeyCodec())
            {
                PauseInstantSaving = true,
            };
            try
            {
                UiLanguage.SetLanguage("zh-CN");
                using var form = new FrmMatrix();
                form.RefreshLanguage();
                form.PerformLayout();
                var tabs = (TabControl)form.Controls.Find("tabControlSetting", true)[0];
                var key = (TextBox)form.Controls.Find("textBoxEnc", true)[0];
                var mapping = (TextBox)form.Controls.Find("textBoxMachineName2IP", true)[0];
                var easyMouse = (ComboBox)form.Controls.Find("comboBoxEasyMouseOption", true)[0];
                var selector = (ComboBox)form.Controls.Find("languageSelector", true)[0];
                var languagePanel = form.Controls.Find("languagePanel", true)[0];
                key.Text = "UNSAVED-PAIRING-KEY";
                mapping.Text = "All 192.168.1.10";
                Assert.IsTrue(tabs.Top >= languagePanel.Bottom, "Language selector overlaps the settings tabs.");
                Assert.AreEqual("电脑设置", tabs.TabPages[0].Text);
                Assert.AreEqual(2, easyMouse.SelectedIndex);
                selector.SelectedIndex = 1;
                Assert.AreEqual("en-US", model.Properties.Language);
                Assert.AreEqual("Machine Setup", tabs.TabPages[0].Text);
                Assert.AreEqual("UNSAVED-PAIRING-KEY", key.Text);
                Assert.AreEqual("All 192.168.1.10", mapping.Text);
                Assert.AreEqual((int)EasyMouseOption.Shift, model.Properties.EasyMouse.Value);
                selector.SelectedIndex = 0;
                Assert.AreEqual("电脑设置", tabs.TabPages[0].Text);
                Assert.AreEqual("UNSAVED-PAIRING-KEY", key.Text);
            }
            finally
            {
                Setting.Values = previous;
            }
        });
    }

    [TestMethod]
    public void SelectingLanguage_DoesNotChangeFormattingCulture()
    {
        var previousLanguage = UiLanguage.Language;
        var previousUi = CultureInfo.CurrentUICulture;
        var previousDefaultUi = CultureInfo.DefaultThreadCurrentUICulture;
        var previousFormatting = CultureInfo.CurrentCulture;
        try
        {
            UiLanguage.SetLanguage("en-US");
            Assert.AreEqual("en-US", CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual(previousFormatting, CultureInfo.CurrentCulture);
            UiLanguage.SetLanguage(null!);
            Assert.AreEqual("zh-CN", UiLanguage.Language);
            Assert.AreEqual("已连接", UiLanguage.Text("Connected"));
        }
        finally
        {
            UiLanguage.SetLanguage(previousLanguage);
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUi;
        }
    }

    private static void OnStaThread(Action action)
    {
        Exception? failure = null;
        var previousLanguage = UiLanguage.Language;
        var previousUi = CultureInfo.DefaultThreadCurrentUICulture;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        try
        {
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "UI checks did not finish.");
            if (failure != null)
            {
                throw failure;
            }
        }
        finally
        {
            UiLanguage.SetLanguage(previousLanguage);
            CultureInfo.DefaultThreadCurrentUICulture = previousUi;
        }
    }
}
