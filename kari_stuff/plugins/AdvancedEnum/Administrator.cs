using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kari.Arguments;
using Kari.GeneratorCore.Workflow;
using Kari.Utils;

namespace Kari.Plugins.AdvancedEnum
{
    public class AdvancedEnumAdministrator : IAdministrator
    {
        [Option("The project which will contain the editor Utilities in Unity")]
        public string advancedEnumCommonUnityProjectName;

        [Option("Whether to generate things related to Unity",
            IsFlag = true)]
        public bool generateUnityThings;

        public AdvancedEnumAnalyzer[] _analyzers;
        public ProjectEnvironmentData CommonUnityPseudoProject { get; private set; }
        
        public void Initialize()
        {
            AdministratorHelpers.Initialize(ref _analyzers);
            var logger = new NamedLogger("AdvancedEnum");

            if (!generateUnityThings && advancedEnumCommonUnityProjectName is not null)
                logger.LogWarning("advancedEnumCommonUnityProjectName should not be defined if generateUnityThings has not been defined.");

            var master = MasterEnvironment.Instance;
            if (advancedEnumCommonUnityProjectName is null)
            {
                CommonUnityPseudoProject = master.RootPseudoProject;
            }
            else
            {
                var p = master.AllProjectDatas.FirstOrDefault(p => p.Name == advancedEnumCommonUnityProjectName);
                if (p is null)
                    logger.LogError($"The required common Unity project {advancedEnumCommonUnityProjectName} has not been found.");
                else
                    CommonUnityPseudoProject = p;
            }
        }
        
        public Task Collect()
        {
            return AdministratorHelpers.CollectAsync(_analyzers);
        }
        
        public Task Generate()
        {
            var master = MasterEnvironment.Instance;

            AdministratorHelpers.AddCodeString(
                master.CommonPseudoProject,
                "AdvancedEnumAnnotations.cs", "AdvancedEnum", DummyAdvancedEnumAnnotations.Text);

            var tasks = new List<Task>();
            if (generateUnityThings)
            {            
                AdministratorHelpers.AddCodeString(
                    CommonUnityPseudoProject,
                    "EnumArrayDrawer.cs", "EnumArray", EnumArrayDrawerSource);

                for (int i = 0; i < _analyzers.Length; i++)
                {
                    var a = _analyzers[i];
                    var p = master.Projects[i].Data;
                    var t = Task.Run(
                        () => a.AddUnityPropertyDrawers(master, p));
                    tasks.Add(t);
                }
            }

            tasks.Add(AdministratorHelpers.GenerateAsync(_analyzers, "AdvancedEnum.cs"));

            return Task.WhenAll(tasks);
        }

        const string EnumArrayDrawerSource =
@"
namespace Kari.Plugins.AdvancedEnum.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System;

    public abstract class EnumArrayDrawer : PropertyDrawer
    {
        protected static float LabelHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        protected abstract string[] Names { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var array = property.FindPropertyRelative(""Values"");
            position.height = LabelHeight;
            bool isExpanded = EditorGUI.Foldout(position, property.isExpanded, label);

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                position.y += LabelHeight;
                DrawArrayWrapperElements(array, Names, ref position);
                EditorGUI.indentLevel--;
            }

            property.isExpanded = isExpanded;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var labelSize = LabelHeight;
            if (!property.isExpanded)
                return labelSize;

            var array = property.FindPropertyRelative(""Values"");
            return labelSize + GetArrayWrapperElementsHeight(array, Names);
        }

        public static float GetArrayWrapperElementsHeight(SerializedProperty property, ReadOnlySpan<string> elementNames)
        {
            property.arraySize = elementNames.Length;

            float result = 0;
            for (int i = 0; i < elementNames.Length; i++)
            {
                var arrayElement = property.GetArrayElementAtIndex(i);
                var height = EditorGUI.GetPropertyHeight(arrayElement, includeChildren: true);
                result += height + EditorGUIUtility.standardVerticalSpacing;
            }

            return result;
        }

        public static void DrawArrayWrapperElements(SerializedProperty property, ReadOnlySpan<string> elementNames, ref Rect rect)
        {
            var content = new GUIContent();
            property.arraySize = elementNames.Length;

            for (int i = 0; i < elementNames.Length; i++)
            {
                string name = elementNames[i];
                content.text = name;
                var arrayElement = property.GetArrayElementAtIndex(i);
                var height = EditorGUI.GetPropertyHeight(arrayElement, includeChildren: true);
                var actualHeight = height + EditorGUIUtility.standardVerticalSpacing;
                rect.height = actualHeight;
                EditorGUI.PropertyField(rect, arrayElement, content);
                rect.y += actualHeight;
            }
        }
    }
}
";
        
        public string GetAnnotations() => DummyAdvancedEnumAnnotations.Text + EnumArrayDrawerSource;
    }
}
