
using System;
using Common.Editor;
using UnityEditor;
using UnityEngine;

namespace Zayats.Unity.View.Editor
{
    public static class Buttons
    {
        [MenuItem("Tools/Create or Select " + nameof(SetupConfiguration))]
        public static void CreateSetupConfiguration()
        {
            AssetDatabaseHelper.FindOrCreateObjectOfType_AndSelectIt<SetupConfiguration>();
        }
    }
}