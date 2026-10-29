#if !LCK_DISABLE_EDITOR_DEPENDENCY_RESOLVER
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liv.Lck.DependencyInjection
{
    [InitializeOnLoad]
    public static class LckInjectDependencyResolverOnInspectorDraw
    {
        private static readonly Dictionary<Type, bool> TypeHasInjectAttributeCache = new Dictionary<Type, bool>();

        static LckInjectDependencyResolverOnInspectorDraw()
        {
            Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
            AssemblyReloadEvents.afterAssemblyReload += ClearCache;
        }

        private static void OnFinishedDefaultHeaderGUI(Editor editor)
        {
            if (Application.isPlaying || editor.target == null)
                return;

            GameObject targetGameObject = editor.target as GameObject;
            if (targetGameObject == null && editor.target is Component component)
            {
                targetGameObject = component.gameObject;
            }

            if (targetGameObject == null)
                return;
            
            if (targetGameObject.GetComponent<LckDependencyResolver>() != null)
                return;

            var monoBehaviours = targetGameObject.GetComponents<MonoBehaviour>();
            foreach (var monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour == null) continue; 
                
                if (TypeHasInjectAttribute(monoBehaviour.GetType()))
                {
                    EnsureLckInjectableComponent(targetGameObject);
                    break; 
                }
            }
        }

        private static void ClearCache()
        {
            TypeHasInjectAttributeCache.Clear();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static bool TypeHasInjectAttribute(Type type)
        {
            if (TypeHasInjectAttributeCache.TryGetValue(type, out bool hasInjectAttribute))
            {
                return hasInjectAttribute;
            }

            hasInjectAttribute = CheckForInjectAttribute(type);
            TypeHasInjectAttributeCache[type] = hasInjectAttribute;
            return hasInjectAttribute;
        }

        private static bool CheckForInjectAttribute(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

            while (type != null && type != typeof(MonoBehaviour) && type != typeof(object))
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (field.GetCustomAttribute<InjectLckAttribute>() != null) return true;
                }

                foreach (var property in type.GetProperties(flags))
                {
                    if (property.GetCustomAttribute<InjectLckAttribute>() != null) return true;
                }

                foreach (var method in type.GetMethods(flags))
                {
                    if (method.GetCustomAttribute<InjectLckAttribute>() != null) return true;
                }
                
                type = type.BaseType;
            }

            return false;
        }

        private static void EnsureLckInjectableComponent(GameObject gameObject)
        {
            if (gameObject.GetComponent<LckDependencyResolver>() == null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (gameObject != null) 
                    {
                        if(gameObject.GetComponent<LckDependencyResolver>() != null)
                            return;
                        
                        Undo.AddComponent<LckDependencyResolver>(gameObject);

                        if (gameObject.scene.IsValid())
                        {
                            EditorSceneManager.MarkSceneDirty(gameObject.scene);
                        }
                    }
                };
            }
        }
    }
}
#endif
