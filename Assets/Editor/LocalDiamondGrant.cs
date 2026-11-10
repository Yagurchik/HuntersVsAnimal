using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Requests live in ignored Library, belong only to this checkout, and are consumed once.
// This is an editor tool, not a source of currency in the shipped game.
[InitializeOnLoad]
public static class LocalDiamondGrant
{
    private const string RequestPath = "Library/LocalDiamondGrant.json";
    private const string ReceiptPath = "Library/LocalDiamondGrantReceipt.json";
    private static double nextCheck;

    [Serializable]
    private sealed class Request
    {
        public string id;
        public int minimumBalance;
    }

    [Serializable]
    private sealed class Receipt
    {
        public string id;
        public int before;
        public int balance;
        public string utc;
    }

    static LocalDiamondGrant()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update += ProcessRequest;
    }

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(RequestPath)) return;
        try
        {
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            if (request == null || string.IsNullOrEmpty(request.id) || request.minimumBalance < 0)
                throw new InvalidDataException("Invalid local diamond request.");
            string marker = "ForestAnimalWar.EditorDiamondGrant." + request.id;
            int before = MetaProgression.Diamonds;
            if (!PlayerPrefs.HasKey(marker))
            {
                PlayerPrefs.SetInt(marker, 1);
                // The previous attempt wrote the registry directly. Synchronize that same grant
                // into Unity's cached wallet; do not add a second 1000 on top of it.
                MetaProgression.SetEditorDiamondBalance(Mathf.Max(before, request.minimumBalance));
            }
            var receipt = new Receipt { id = request.id, before = before,
                balance = MetaProgression.Diamonds, utc = DateTime.UtcNow.ToString("O") };
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
            File.Delete(RequestPath);
            Debug.Log("Local diamond balance updated through Unity: " + receipt.balance);
        }
        catch (Exception error) { Debug.LogException(error); }
    }
}
