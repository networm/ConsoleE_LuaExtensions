using UnityEditor;
using UnityEngine;

namespace ConsoleE
{
    public class ConsoleE_Extensions_Option : ScriptableObject
    {
        private const string OptionPath = "Assets/Editor/ConsoleE_LuaExtensions/ConsoleE_Extensions_Option.asset";

        public enum OpenEditor
        {
            UnityInternal,
            AssociatedByExtension,
            CustomEditorPath,
        }

        [Header("Method")]
        [SerializeField]
        public OpenEditor CurrentOpenEditor;

        [Header("Path")]
        [SerializeField]
        public string CurrentOpenEditorPath = @"C:\Program Files\JetBrains\IntelliJ IDEA Community Edition 2019.2.4\bin\idea64.exe";

        [Header("Format {0} is path, {1} is lineNumber")]
        [SerializeField]
        public string ArgumentPattern = "--line {1} {0}";

        [Header("Format {0} is lua path")]
        [SerializeField]
        public string LuaPathPattern = "Assets/Lua/{0}";

        [MenuItem("Tools/ConsoleE/Create Option")]
        public static void CreateOption()
        {
            var asset = CreateInstance<ConsoleE_Extensions_Option>();
            AssetDatabase.CreateAsset(asset, OptionPath);
        }

        public static bool HasOption()
        {
            string guid = AssetDatabase.AssetPathToGUID(OptionPath);
            return !string.IsNullOrEmpty(guid);
        }

        public static ConsoleE_Extensions_Option Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (!HasOption())
                    {
                        CreateOption();
                    }
                    _instance = AssetDatabase.LoadAssetAtPath<ConsoleE_Extensions_Option>(OptionPath);
                }

                return _instance;
            }
        }

        private static ConsoleE_Extensions_Option _instance;
    }
}
