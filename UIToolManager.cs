using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Reflection;

namespace com.tencent.pandora.tools
{
    public class UIToolManager
    {


        static public T GUIDToObject<T>(string guid) where T : Object
        {
            Object obj = GUIDToObject(guid);
            if (obj == null)
            {
                return null;
            }
            System.Type objType = obj.GetType();
            if (objType == typeof(T) || objType.IsSubclassOf(typeof(T)))
            {
                return obj as T;
            }

            if (objType == typeof(GameObject) && typeof(T).IsSubclassOf(typeof(Component)))
            {
                GameObject go = obj as GameObject;
                return go.GetComponent(typeof(T)) as T;
            }
            return null;
        }

        static public Object GUIDToObject(string guid)
        {
            if (string.IsNullOrEmpty(guid) == true)
            {
                return null;
            }

            MethodInfo getInstanceIdFromGUID = typeof(AssetDatabase).GetMethod("GetInstanceIDFromGUID", BindingFlags.Static | BindingFlags.NonPublic);
            int id = (int)getInstanceIdFromGUID.Invoke(null, new object[] { guid });
            if (id != 0)
            {
                return EditorUtility.InstanceIDToObject(id);
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) == true)
            {
                return null;
            }
            //return AssetDatabase.LoadAssetAtPath<Object>(path);
            return AssetDatabase.LoadAssetAtPath(path,typeof(Object)) as Object ;

        }

        static public string ObjectToGUID(Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}