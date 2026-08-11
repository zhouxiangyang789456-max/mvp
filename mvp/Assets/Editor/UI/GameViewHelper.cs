using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorUI
{
    /// <summary>
    /// Sets the Game view to a specific resolution (reflection over Unity's internal GameView API).
    /// Menu: Tools/MVP/UI/Set Game View 1600x900
    /// </summary>
    public static class GameViewHelper
    {
        [MenuItem("Tools/MVP/UI/Set Game View 1600x900")]
        public static void Set1600x900()
        {
            SetCustomSize(1600, 900);
            Debug.Log("[GameViewHelper] Game view set to 1600x900");
        }

        [MenuItem("Tools/MVP/UI/Capture Game View")]
        public static void CaptureGameView()
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Captures");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "BattleUI_Actual.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[GameViewHelper] Capturing Game view to " + path);
        }



        public static void SetCustomSize(int width, int height)
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) { Debug.LogError("[GameViewHelper] GameView type not found"); return; }

            var gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null) { Debug.LogError("[GameViewHelper] No GameView window"); return; }

            var sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instanceProp = singleType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            var sizesInstance = instanceProp.GetValue(null, null);

            var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
            // 0 == GameViewSizeGroupType.Standalone
            var group = getGroup.Invoke(sizesInstance, new object[] { 0 });
            var groupType = group.GetType();

            var addCustom = groupType.GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
            var sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
            object custom = null;
            var constructors = sizeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length != 4 || parameters[1].ParameterType != typeof(int) ||
                    parameters[2].ParameterType != typeof(int) || parameters[3].ParameterType != typeof(string))
                    continue;

                object fixedResolution = parameters[0].ParameterType.IsEnum
                    ? System.Enum.ToObject(parameters[0].ParameterType, 1)
                    : System.Convert.ChangeType(1, parameters[0].ParameterType);
                custom = constructor.Invoke(new object[]
                {
                    fixedResolution, width, height, width + "x" + height
                });
                break;
            }
            if (custom == null)
            {
                Debug.LogError("[GameViewHelper] Compatible GameViewSize constructor not found");
                return;
            }

            addCustom.Invoke(group, new object[] { custom });

            var getCustomCount = groupType.GetMethod("GetCustomCount", BindingFlags.Public | BindingFlags.Instance);
            var getBuiltinCount = groupType.GetMethod("GetBuiltinCount", BindingFlags.Public | BindingFlags.Instance);
            int customCount = (int)getCustomCount.Invoke(group, null);
            int builtinCount = (int)getBuiltinCount.Invoke(group, null);
            int total = builtinCount + customCount;

            var selMethod = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.NonPublic | BindingFlags.Instance);
            if (selMethod != null)
            {
                selMethod.Invoke(gameView, new object[] { total - 1, null });
            }
            else
            {
                // fallback: set via SetSize internal
                var setSize = gameViewType.GetMethod("SetSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (setSize != null) setSize.Invoke(gameView, new object[] { total - 1, width, height, null });
            }

            gameView.Repaint();
        }
    }
}
