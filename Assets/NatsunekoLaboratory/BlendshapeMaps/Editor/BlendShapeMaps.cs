using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.BlendshapeMaps
{
    public class BlendShapeMaps : EditorWindow
    {
        private const string StyleGuid = "9ff39a89a7fd440a83baf6df7f9d9da8";
        private const string XamlGuid = "a7b31afadb4c4362905684e325077a3b";

        [SerializeField] private Transform _transform;
        [SerializeField] private SkinnedMeshRenderer _renderer;
        [SerializeField] private List<AnimationClip> _animations = new List<AnimationClip>();
        [SerializeField] private List<AnimatorController> _controllers = new List<AnimatorController>();

        private SerializedObject _so;
        private Dictionary<string, string> _maps = new Dictionary<string, string>();
        
        [MenuItem("Window/Natsuneko Laboratory/BlendShape Maps")]
        private static void ShowWindow()
        {
            var window = GetWindow<BlendShapeMaps>();
            window.titleContent = new GUIContent("BlendShape Maps");
            window.Show();
        }

        private static T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void OnEnable()
        {
            _so = new SerializedObject(this);
        }

        private void CreateGUI()
        {
            _so.Update();
            
            rootVisualElement.styleSheets.Add(LoadAssetByGuid<StyleSheet>(StyleGuid));

            var xaml = LoadAssetByGuid<VisualTreeAsset>(XamlGuid);
            var tree = xaml.CloneTree();
            tree.Bind(_so);
            rootVisualElement.Add(tree);

            var container = tree.Query<VisualElement>("container").First();
            
            var renderer = tree.Query<PropertyField>("renderer").First();
            renderer.RegisterValueChangeCallback(_ =>
            {
                if (_renderer == null)
                    return;
                
                var mesh = _renderer.sharedMesh;
                var indices = mesh.blendShapeCount;
                container.Clear();
                _maps.Clear();

                for (var i = 0; i < indices; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    var elem = new VisualElement().AddClass("border", "border-neutral-700", "px-2", "py-1", "my-1");

                    var text = new TextElement { text = $"{name} to ..." }.AddClass("ml-1");
                    elem.Add(text);

                    var input = new TextField() { value = name }.AddClass("mt-2", "mb-1");
                    input.RegisterValueChangedCallback(e =>
                    {
                        if (_maps.ContainsKey(name))
                        {
                            _maps[name] = e.newValue;
                        }
                    });
                    
                    elem.Add(input);
                    container.Add(elem);
                    
                    _maps.Add(name, name);
                }
            });
            
            var submit = rootVisualElement.Query<Button>("submit").First();
            submit.clicked += () =>
            {
                submit.SetEnabled(false);

                var animations = new List<AnimationClip>(_animations);
                animations.AddRange(_controllers.SelectMany(w => w.animationClips));

                var changes = new Dictionary<string, string>();
                foreach (var map in _maps)
                {
                    if (map.Key != map.Value)
                        changes.Add(map.Key, map.Value);
                }

                if (changes.Count == 0)
                    return;
                
                EditorUtility.DisplayProgressBar("BlendShape Map", "Mapping......", 0);
                
                foreach (var (animation, index) in animations.Select((w, i) => (w, i)))
                {
                    EditorUtility.DisplayProgressBar("BlendShape Map", "Mapping......", (float)index / animations.Count);
                    ApplyChangesToAnimation(animation, changes);
                }
                
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                submit.SetEnabled(true);
            };
        }

        private void OnGUI()
        {
            var submit = rootVisualElement.Query<Button>("submit").First();
            var disabled = (_animations.Count == 0 && _controllers.Count == 0) || _transform == null;
            submit.SetEnabled(!disabled);
        }

        private  void ApplyChangesToAnimation(AnimationClip animation, Dictionary<string, string> changes)
        {
            var root = _transform;
            var target = _renderer.transform;
            var path = AnimationUtility.CalculateTransformPath(target, root);
            var bindings = AnimationUtility.GetCurveBindings(animation);
            var newBindings = new List<(EditorCurveBinding, AnimationCurve)>();
            
            foreach (var binding in bindings)
            {
                var newBinding = binding;
                if (binding.path == path)
                {
                    var actualName = binding.propertyName.Substring("blendShape.".Length);
                    if (changes.TryGetValue(actualName, out var newName))
                    {
                        newBinding.propertyName = $"blendShape.{newName}";
                        Debug.Log($"The attribute changed from {binding.propertyName} to {newBinding.propertyName}");
                    }
                }

                newBindings.Add((newBinding, AnimationUtility.GetEditorCurve(animation, binding)));
            }
            
            animation.ClearCurves();
            foreach (var (binding, curve) in newBindings)
                AnimationUtility.SetEditorCurve(animation, binding, curve);
            
            AssetDatabase.SaveAssets();
        }
    }

    public static class VisualElementExtensions
    {
        public static T AddClass<T>(this T obj, params string[] classes) where T : VisualElement
        {
            foreach (var @class in classes)
            {
                obj.AddToClassList(@class);
            }

            return obj;
        }
    }
}
