using System;
using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEngine;
using Voxon;
using Object = UnityEngine.Object;
using UnityObject = UnityEngine.Object;
// ReSharper disable CheckNamespace

public abstract class DictionaryDrawer<TK, TV> : PropertyDrawer where TV : Enum
{
    private IntKeyCodeStringPairDictionary<TV> _intDictionary;
    private SerializableDictionary<TK, TV> _dictionary;
    private bool _foldout;

    private int lineHeight = 19;

    private const float KButtonWidth = 18f;
    private int addEntryIntIndex = 0;

    private int selectedEditIndex = -1;
    private string originalKey = "";
    private TV originalValue = default(TV);

    private string rememberedEditKey = "";
    private TV rememberedEditValue = default(TV);


    private string rememberedAddKey = "";
    private TV rememberedAddValue = default(TV);

    private bool DEBUG = false;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        CheckInitialize(property, label);
        if (_foldout)
            return (_dictionary.Count + 4) * lineHeight;
        return lineHeight;
    }
 
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CheckInitialize(property, label);
        position.height = lineHeight;
        Rect foldoutRect = position;
        foldoutRect.width -= 2 * KButtonWidth;

        _foldout = EditorGUI.Foldout(foldoutRect, _foldout, label, true);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetBool(label.text, _foldout);

        var buttonRect = position;
        buttonRect.width = 75;
        buttonRect.x = position.width - buttonRect.width + position.x;
        buttonRect.x -= KButtonWidth;

        if (GUI.Button(buttonRect, new GUIContent("Clear", "Clear all inputs for this device"), EditorStyles.miniButtonRight))
        {
            ClearDictionary();
            if(DEBUG)Debug.Log("Dictionary Cleared");
            return;
        }

        if (!_foldout)
            return;
        position.y += lineHeight;
        Rect header = position;
        header.x += 97;
        EditorGUI.LabelField(header, $"String / Key Value");
        header.x += 280;
        EditorGUI.LabelField(header, $"Input Keycode");

       

        int i = 0;
        _intDictionary = new IntKeyCodeStringPairDictionary<TV>();
        List<String> alreadyAssignedKeys = new List<String>();
        foreach (KeyValuePair<TK, TV> item in _dictionary)
        {
            if (item.Key is string key && item.Value is TV value && typeof(TV).IsEnum)
            {
                alreadyAssignedKeys.Add(key);
                var lastPair = new KeyCodeStringPair<TV>(key, value);
                _intDictionary.Add(i, lastPair);
                i++;
            }
            else
            {
                if (DEBUG)Debug.LogWarning($"Key or Value type mismatch. Expected string and enum, but got key type: {item.Key.GetType()} and value type: {item.Value.GetType()}.");
            }
        }

        addEntryIntIndex = i + 1;
        _intDictionary.Add(addEntryIntIndex, new KeyCodeStringPair<TV>(rememberedAddKey, rememberedAddValue));

        position.x = 100;

        Rect errorMsg = position;
        errorMsg.y = foldoutRect.y;
        errorMsg.x = 97;
        GUIStyle errorStyle = new GUIStyle(EditorStyles.label);
        errorStyle.normal.textColor = Color.red;
        GUIStyle editStyle = new GUIStyle(EditorStyles.label);
        editStyle.normal.textColor = Color.yellow;


        foreach (KeyValuePair<int, KeyCodeStringPair<TV>> item in _intDictionary)
        {
            int key = item.Key;
            KeyCodeStringPair<TV> value = item.Value;

            position.y += 20f;

            Rect msg = position;
            msg.x = 15f;
            msg.width = 60;

            if (key == addEntryIntIndex)
            {
                position.y += 5;
                msg.y = position.y;
                EditorGUI.LabelField(msg, "New Entry", editStyle);
            } else if (selectedEditIndex != key)
            {
                msg.y = position.y;
                if (GUI.Button(msg, new GUIContent("Edit", "Add to dictionary"), EditorStyles.miniButtonLeft))
                {
                    selectedEditIndex = key;
                    originalKey = value.SearchStr;
                    originalValue = value.KeyCode;
                    rememberedEditKey = originalKey;
                    rememberedEditValue = originalValue;
                }

            } else if (selectedEditIndex == key)
            {
                if (alreadyAssignedKeys.Contains(rememberedEditKey) && rememberedEditKey != originalKey)
                {
                    EditorGUI.LabelField(errorMsg, $"Error - {rememberedEditKey} already exists. Entries must be unique", errorStyle);
                }
                msg.y = position.y;
                if (GUI.Button(msg, new GUIContent("Update", "Add to dictionary"), EditorStyles.miniButtonLeft))
                {
                    try
                    {
                        if (alreadyAssignedKeys.Contains(rememberedEditKey) && rememberedEditKey != originalKey)
                        {
                            Debug.LogError($"Voxon Input Manager Error - {rememberedEditKey} already exists. Entries must be uniqu");
                       
                            return;
                        }

                        _dictionary.Remove((TK)(object)originalKey);
                        _dictionary.Add((TK)(object)rememberedEditKey, (TV)(object)rememberedEditValue);
                        rememberedEditKey = "";
                        rememberedEditValue = default(TV);
                        selectedEditIndex = -1;
                        if (DEBUG) Debug.Log($"Added Saved - Key {rememberedEditKey} / Value {rememberedEditValue} to dictionary");

                        break;

                    } catch (Exception e)
                    {
                        Debug.LogError($"couldn't update field why? {e.Message}");
                    }
                }
            }


            Rect keyRect = position;
            keyRect.width /= 3;
            keyRect.width -= 5;
  
            if (selectedEditIndex == key && rememberedEditKey != "")
            {
                value.SearchStr = rememberedEditKey;
            }

                // STR KEY  CHECK -- STRING
            if (selectedEditIndex == key || key == addEntryIntIndex)
            {

                EditorGUI.BeginChangeCheck();
            
                value.SearchStr = EditorGUI.TextField(keyRect, value.SearchStr);
                if (EditorGUI.EndChangeCheck()) // Check if value changed
                {
                    if (key == addEntryIntIndex)
                    {
                        rememberedAddKey = value.SearchStr;
                    }
                    if (selectedEditIndex == key)
                    {
                        rememberedEditKey = value.SearchStr;
                    }

                }
            }
            else
            {
                EditorGUI.LabelField(keyRect, value.SearchStr);

            }

            Rect valueRect = position;
            valueRect.x = position.x + position.width / 3 + 15;
            valueRect.width = keyRect.width - KButtonWidth;

            // ENUM  VALUE -- CHECK ENUM

            if (selectedEditIndex == key)
            {
                value.KeyCode = rememberedEditValue;
            }


            if (selectedEditIndex == key || key == addEntryIntIndex)
            {

                EditorGUI.BeginChangeCheck();
                value.KeyCode = (TV)(object)EditorGUI.EnumPopup(valueRect, (Enum)(object)value.KeyCode);
                if (EditorGUI.EndChangeCheck()) // Check if value changed
                {
                    if (key == addEntryIntIndex)
                    {
                        rememberedAddValue = value.KeyCode;
                    }
                    if (selectedEditIndex == key)
                    {
                        rememberedEditValue = value.KeyCode;
                    }
                }
            } else
            {
                EditorGUI.LabelField(valueRect, value.KeyCode.ToString());
            }

            Rect removeRect = valueRect;
            removeRect.x = valueRect.xMax + 15;
            removeRect.width = KButtonWidth;

            errorMsg.y = valueRect.y + 20;

            if (key == addEntryIntIndex)
            {

                if (alreadyAssignedKeys.Contains(value.SearchStr) && value.SearchStr != originalKey)
                {
                    EditorGUI.LabelField(errorMsg, $"String/Key already exists. Add a unique entry", editStyle);
                }

                    removeRect.width = KButtonWidth + 20;
                if (GUI.Button(removeRect, new GUIContent("Add", "Add to dictionary"), EditorStyles.miniButtonRight))
                {

                    if (alreadyAssignedKeys.Contains(value.SearchStr) && value.SearchStr != originalKey)
                    {
                    
                        Debug.LogError($"Voxon Input Manager Error - All String/Key entries already exists. all entries must be unique");
                        return;
                    }

                    if (DEBUG) Debug.Log($"Dictionary count {_dictionary.Count}");
                    _dictionary.Add((TK)(object)value.SearchStr, value.KeyCode);
                    if (DEBUG) Debug.Log($"ADD Dictionary count {_dictionary.Count}");
                    rememberedAddKey = "";
                    rememberedAddValue = default(TV);
                }
            }
            else if (key == selectedEditIndex)
            {
                removeRect.width = KButtonWidth + 35;
                if (GUI.Button(removeRect, new GUIContent("Cancel", "Cancel Edit"), EditorStyles.miniButtonRight))
                {
                    rememberedEditKey = "";
                    rememberedEditValue = default(TV);
                    selectedEditIndex = -1;
                }
            }
            else
            {
                removeRect.width = KButtonWidth + 35;
                if (GUI.Button(removeRect, new GUIContent("Remove", "Remove item"), EditorStyles.miniButtonRight))
                {
                    if (DEBUG) Debug.Log("Remove Item Clicked");
                    _dictionary.Remove((TK)(object)value.SearchStr);
                }

                removeRect.x += 60;
                if (GUI.Button(removeRect, new GUIContent("Copy", "Remove item"), EditorStyles.miniButtonRight))
                {
               
                    rememberedAddKey = value.SearchStr;
                    rememberedAddValue = value.KeyCode;
                }
            }
        }
        // Adjust the height of the position to avoid overlap
        position.y += lineHeight;
    }
    private void RemoveItem(TK key)
    {
        _dictionary.Remove(key);
    }

    private void RemoveItemIntDict(int key)
    {
        _intDictionary.Remove(key);
    }


    private void CheckInitialize(SerializedProperty property, GUIContent label)
    {
        if (_dictionary != null) return;
        
        Object target = property.serializedObject.targetObject;
        _dictionary = fieldInfo.GetValue(target) as SerializableDictionary<TK, TV>;
        if (_dictionary == null)
        {
            _dictionary = new SerializableDictionary<TK, TV>();
            fieldInfo.SetValue(target, _dictionary);
        }

        _foldout = EditorPrefs.GetBool(label.text);
    }

    private static readonly Dictionary<Type, Func<Rect, object, object>> _Fields =
        new Dictionary<Type, Func<Rect, object, object>>()
        {
            { typeof(int), (rect, value) => EditorGUI.IntField(rect, (int)value) },
            { typeof(float), (rect, value) => EditorGUI.FloatField(rect, (float)value) },
            { typeof(string), (rect, value) => EditorGUI.TextField(rect, (string)value) },
            { typeof(bool), (rect, value) => EditorGUI.Toggle(rect, (bool)value) },
            { typeof(Vector2), (rect, value) => EditorGUI.Vector2Field(rect, GUIContent.none, (Vector2)value) },
            { typeof(Vector3), (rect, value) => EditorGUI.Vector3Field(rect, GUIContent.none, (Vector3)value) },
            { typeof(Bounds), (rect, value) => EditorGUI.BoundsField(rect, (Bounds)value) },
            { typeof(Rect), (rect, value) => EditorGUI.RectField(rect, (Rect)value) },
        };


    private static T DoFieldKey<T, TEnum>(Rect rect, Type type, T value) where TEnum : Enum
    {
        if (value is KeyCodeStringPair<TEnum> pair)
        {
            // Display the text field for the SearchStr property
            pair.SearchStr = EditorGUI.TextField(rect, pair.SearchStr);
            return (T)(object)pair;
        }

        Debug.Log("Type is not supported: " + type);
        return value;
    }



    private static T DoField<T>(Rect rect, Type type, T value)
    {
        Func<Rect, object, object> field;
        if (_Fields.TryGetValue(type, out field))
            return (T)field(rect, value);

        if (type.IsEnum)
            return (T)(object)EditorGUI.EnumPopup(rect, (Enum)(object)value);

        if (typeof(UnityObject).IsAssignableFrom(type))
            return (T)(object)EditorGUI.ObjectField(rect, (UnityObject)(object)value, type, true);

        Debug.Log("Type is not supported: " + type);
        return value;
    }



    private void ClearDictionary()
    {
        _dictionary.Clear();
    }

    private void AddNewItem()
    {
        TK key;
        if (typeof(TK) == typeof(string))
            key = (TK)(object)"";
        else key = default(TK);

        TV value = default(TV);
        try
        {
            if(key == null) throw new NullReferenceException();
            _dictionary.Add(key, value);
            Debug.Log($"Entry Created = Key= {key} , value={value} count ={_dictionary.Count}");
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private void AddNewItemInt()
    {
        try
        {
            KeyCodeStringPair<TV> value = new KeyCodeStringPair<TV>("", default(TV));
            _intDictionary.Add(addEntryIntIndex, value);
            addEntryIntIndex++;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }
}

[CustomPropertyDrawer(typeof(KeyBindings))]
public class MyDictionaryDrawer1 : DictionaryDrawer<string, Keys> { }

[CustomPropertyDrawer(typeof(ButtonBindings))]
public class MyDictionaryDrawer2 : DictionaryDrawer<string, Buttons> { }

[CustomPropertyDrawer(typeof(AxisBindings))]
public class MyDictionaryDrawer3 : DictionaryDrawer<string, Axis> { }

[CustomPropertyDrawer(typeof(MouseBindings))]
public class MyDictionaryDrawer4 : DictionaryDrawer<string, Mouse_Button> { }

[CustomPropertyDrawer(typeof(SpaceNavBindings))]
public class MyDictionaryDrawer5 : DictionaryDrawer<string, SpaceNav_Button> { }