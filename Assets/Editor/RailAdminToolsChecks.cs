#if UNITY_EDITOR
using System;
using System.Reflection;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public static class RailAdminToolsChecks
{
    [MenuItem("Duck Rails/Legacy/Run Admin Pause Checks")]
    public static void Run()
    {
        if (Application.isPlaying || PhotonNetwork.InRoom) throw new Exception("Run in disconnected Edit Mode.");
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var sessionProperty = typeof(PlayerSession).GetProperty("Instance");
        var adminProperty = typeof(PlayerSession).GetProperty("IsAdmin");
        var previous = PlayerSession.Instance;
        var account = new GameObject("Admin pause check account");
        var session = account.AddComponent<PlayerSession>();
        var menuRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PauseMenu/PauseMenu.prefab"));
        try
        {
            sessionProperty.SetValue(null, session);
            RailAdminTools.Refresh();
            Check(!RailAdminTools.Available, "Editor does not bypass account admin check");
            RailAdminTools.ToggleNoClip(); RailAdminTools.ToggleInvincibility();
            Check(!RailAdminTools.NoClip && !RailAdminTools.Invincible, "Non-admin actions rejected");
            var menu = menuRoot.GetComponent<PauseMenu>();
            Check(menu.testButton != null && menu.testPage != null, "Serialized tab/page references");
            Check(!menu.testButton.gameObject.activeSelf && !menu.testPage.activeSelf, "Admin controls hidden by default");
            var buttons = menu.testPage.GetComponentsInChildren<PauseMenuButton>(true);
            Check(buttons.Length == 4, "Exactly four page buttons");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            foreach (var button in buttons)
            {
                Check(button.momentary && !button.selectOnEnable, "Actions are momentary");
                var text = button.GetComponentInChildren<TMP_Text>();
                Check(text != null && text.font == font, "CutePixel label");
                Check(button.GetComponentInChildren<BoxCollider>() != null, "Physical button collider");
            }
            menu.mesh.SetActive(true);
            typeof(PauseMenu).GetMethod("BindTestButtons", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, null);
            menu.SetActiveButton(menu.testButton.GetComponent<PauseMenuButton>());
            Check(!menu.testPage.activeSelf, "Non-admin cannot select page");
            adminProperty.SetValue(session, true);
            RailAdminTools.Refresh();
            menu.testButton.gameObject.SetActive(true);
            menu.SetActiveButton(menu.testButton.GetComponent<PauseMenuButton>());
            Check(menu.testPage.activeSelf && !menu.cameraPage.activeSelf && !menu.settingsMenu.gameObject.activeSelf, "Test page replaces other page contents");
            var flight = menu.testPage.transform.Find("NoClip").GetComponent<PauseMenuButton>();
            var god = menu.testPage.transform.Find("Invincibility").GetComponent<PauseMenuButton>();
            flight.Press(); Check(RailAdminTools.NoClip, "No Clip button enables flight");
            flight.Release(); flight.Press(); flight.Release(); Check(!RailAdminTools.NoClip, "No Clip button disables flight");
            god.Press(); god.Release(); Check(RailAdminTools.Invincible, "Invincibility button enables protection");
            god.Press(); god.Release(); Check(!RailAdminTools.Invincible, "Invincibility button disables protection");
            flight.Press(); flight.Release(); god.Press(); god.Release();
            adminProperty.SetValue(session, false);
            Check(!RailAdminTools.NoClip && !RailAdminTools.Invincible, "Revocation gates active cheats immediately");
            RailAdminTools.Refresh();
            adminProperty.SetValue(session, true);
            Check(!RailAdminTools.NoClip && !RailAdminTools.Invincible, "Revocation clears retained toggles");
            var one = new Hashtable(); var fifty = new Hashtable();
            RailExpedition.ApplyCoalFuel(one, 1, 1); RailExpedition.ApplyCoalFuel(fifty, 1, 50);
            Check(Mathf.Approximately((float)one[RailSessionRules.Fuel], RailResourceBalance.StartingFuel + RailResourceBalance.CoalValue(1)), "One coal uses normal resource value");
            Check((float)fifty[RailSessionRules.Fuel] == RailResourceBalance.Capacity, "Fifty coal respects fuel capacity");
            var motion = (object[])fifty[RailSessionRules.Motion];
            Check((float)motion[1] == 0 && (float)motion[2] == 0 && (float)motion[3] == 0, "Refuelling preserves stopped motion");
            Debug.Log("ADMIN PAUSE: " + checks + " EditMode checks passed");
        }
        finally
        {
            sessionProperty.SetValue(null, previous);
            RailAdminTools.Refresh();
            UnityEngine.Object.DestroyImmediate(menuRoot);
            UnityEngine.Object.DestroyImmediate(account);
        }
    }
}
#endif
