using System;
using UnityEngine;

/// <summary>
/// Per-user wrapper around Unity PlayerPrefs for shared-headset support on Meta Quest.
/// Keys are scoped to the active Meta org-scoped user id. Legacy unscoped keys migrate on first read.
/// </summary>
public static class UserPlayerPrefs {
	const string EditorUserId = "editor";

	static string currentUserId;

	public static bool IsReady => !string.IsNullOrEmpty(currentUserId);
	public static string CurrentUserId => currentUserId;
	public static event Action UserReady;

	//[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void InitializeEditorUser() {
#if UNITY_EDITOR
		SetCurrentUser(EditorUserId);
#endif
	}

	public static void SetCurrentUser(string userId) {
		if (string.IsNullOrEmpty(userId)) {
			return;
		}

		if (currentUserId == userId) {
			return;
		}

		currentUserId = userId;
		UserReady?.Invoke();
	}

	public static string ScopedKey(string key) {
		if (string.IsNullOrEmpty(currentUserId)) {
			{
			    if (DuckzLog.AllowWarning(out var logNotice1))
			        logNotice1.Write($"UserPlayerPrefs accessed before user scope was set: {key}");
			}
			return key;
		}

		return "u." + currentUserId + "." + key;
	}

	public static bool HasKey(string key) {
		string scopedKey = ScopedKey(key);
		return PlayerPrefs.HasKey(scopedKey) || PlayerPrefs.HasKey(key);
	}

	public static void DeleteKey(string key) {
		PlayerPrefs.DeleteKey(ScopedKey(key));
		PlayerPrefs.DeleteKey(key);
	}

	public static void Save() {
		PlayerPrefs.Save();
	}

	public static int GetInt(string key, int defaultValue = 0) {
		string scopedKey = ScopedKey(key);
		if (PlayerPrefs.HasKey(scopedKey)) {
			return PlayerPrefs.GetInt(scopedKey, defaultValue);
		}

		if (PlayerPrefs.HasKey(key)) {
			int value = PlayerPrefs.GetInt(key, defaultValue);
			SetInt(key, value);
			PlayerPrefs.DeleteKey(key);
			return value;
		}

		return defaultValue;
	}

	public static void SetInt(string key, int value) {
        string scopedKey = ScopedKey(key);
        PlayerPrefs.SetInt(scopedKey, value);
        // Before sign-in, the unscoped key is the value we just wrote.
        if (scopedKey != key)
            PlayerPrefs.DeleteKey(key);
	}

	public static float GetFloat(string key, float defaultValue = 0f) {
		string scopedKey = ScopedKey(key);
		if (PlayerPrefs.HasKey(scopedKey)) {
			return PlayerPrefs.GetFloat(scopedKey, defaultValue);
		}

		if (PlayerPrefs.HasKey(key)) {
			float value = PlayerPrefs.GetFloat(key, defaultValue);
			SetFloat(key, value);
			PlayerPrefs.DeleteKey(key);
			return value;
		}

		return defaultValue;
	}

	public static void SetFloat(string key, float value) {
		string scopedKey = ScopedKey(key);
        PlayerPrefs.SetFloat(scopedKey, value);
        // Before sign-in, the unscoped key is the value we just wrote.
        if (scopedKey != key)
            PlayerPrefs.DeleteKey(key);
	}

	public static string GetString(string key, string defaultValue = "") {
		string scopedKey = ScopedKey(key);
		if (PlayerPrefs.HasKey(scopedKey)) {
			return PlayerPrefs.GetString(scopedKey, defaultValue);
		}

		if (PlayerPrefs.HasKey(key)) {
			string value = PlayerPrefs.GetString(key, defaultValue);
			SetString(key, value);
			PlayerPrefs.DeleteKey(key);
			return value;
		}

		return defaultValue;
	}

	public static void SetString(string key, string value) {
		string scopedKey = ScopedKey(key);
        PlayerPrefs.SetString(scopedKey, value);
        // Before sign-in, the unscoped key is the value we just wrote.
        if (scopedKey != key)
            PlayerPrefs.DeleteKey(key);
	}

    public static void SetStringArray(string key, string[] value) {
        string scopedKey = ScopedKey(key);
        PlayerPrefsX.SetStringArray(scopedKey, value);
        // Before sign-in, the unscoped key is the value we just wrote.
        if (scopedKey != key)
            PlayerPrefs.DeleteKey(key);
    }

    public static string[] GetStringArray(string key) {
        string scopedKey = ScopedKey(key);
        return PlayerPrefsX.GetStringArray(scopedKey);
    }
}
