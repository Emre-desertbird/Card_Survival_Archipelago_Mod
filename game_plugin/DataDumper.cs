using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Writes every game ScriptableObject (cards, stats, objectives, perks, ...) as
    /// JSON, one file per type, then quits. Unlike JsonUtility it keeps references
    /// between objects readable (name, type and UniqueID instead of an instance id)
    /// and leaves out default values, so the audit tools can follow the data.
    /// </summary>
    internal static class DataDumper
    {
        private const int MaxDepth = 12;
        private static readonly Assembly GameAssembly = typeof(GameManager).Assembly;
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        public static IEnumerator Run(string directory)
        {
            float waited = 0f;
            // Wait until the game has loaded its data base and save slots (main menu ready).
            while ((GameLoad.Instance == null || GameLoad.Instance.Games == null || GameLoad.Instance.Games.Count < 4 ||
                    UnityEngine.Object.FindObjectOfType<MainMenu>() == null) && waited < 120f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            try
            {
                Dump(directory);
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("Data dump failed: " + error);
                File.WriteAllText(Path.Combine(directory, "error.txt"), error.ToString());
            }
            Application.Quit();
        }

        private static void Dump(string directory)
        {
            Directory.CreateDirectory(directory);
            var byType = new SortedDictionary<string, List<ScriptableObject>>(StringComparer.Ordinal);
            foreach (ScriptableObject entry in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (entry == null || entry.GetType().Assembly != GameAssembly)
                {
                    continue;
                }
                string typeName = entry.GetType().Name;
                if (!byType.ContainsKey(typeName))
                {
                    byType.Add(typeName, new List<ScriptableObject>());
                }
                byType[typeName].Add(entry);
            }

            var manifest = new StringBuilder();
            manifest.Append("{\"game_version\":").Append(Quote(Application.version));
            manifest.Append(",\"unity_version\":").Append(Quote(Application.unityVersion));
            manifest.Append(",\"plugin_version\":").Append(Quote(Plugin.Version));
            manifest.Append(",\"save_folder\":").Append(Quote(Application.persistentDataPath));
            manifest.Append(",\"saves_isolated\":").Append(SaveIsolation.IsIsolated ? "true" : "false");
            manifest.Append(",\"types\":{");
            bool firstType = true;
            foreach (KeyValuePair<string, List<ScriptableObject>> group in byType)
            {
                group.Value.Sort((left, right) => string.CompareOrdinal(SortKey(left), SortKey(right)));
                var json = new StringBuilder("[\n");
                for (int i = 0; i < group.Value.Count; i++)
                {
                    if (i > 0)
                    {
                        json.Append(",\n");
                    }
                    WriteRoot(json, group.Value[i]);
                }
                json.Append("\n]\n");
                File.WriteAllText(Path.Combine(directory, group.Key + ".json"), json.ToString(), new UTF8Encoding(false));
                if (!firstType)
                {
                    manifest.Append(",");
                }
                firstType = false;
                manifest.Append(Quote(group.Key)).Append(":").Append(group.Value.Count);
            }
            manifest.Append("}}\n");
            File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToString(), new UTF8Encoding(false));
            Plugin.Log.LogInfo("Game data dumped to " + directory);
        }

        private static string SortKey(ScriptableObject entry)
        {
            var unique = entry as UniqueIDScriptable;
            return entry.name + "|" + (unique != null ? unique.UniqueID : "");
        }

        private static void WriteRoot(StringBuilder json, ScriptableObject entry)
        {
            json.Append("{\"$name\":").Append(Quote(entry.name));
            json.Append(",\"$type\":").Append(Quote(entry.GetType().Name));
            WriteFields(json, entry, 0, false);
            json.Append("}");
        }

        /// <summary>Appends ,"field":value for every non-default serialized field.</summary>
        private static void WriteFields(StringBuilder json, object owner, int depth, bool first)
        {
            foreach (FieldInfo field in SerializedFields(owner.GetType()))
            {
                object value;
                try
                {
                    value = field.GetValue(owner);
                }
                catch (Exception)
                {
                    continue;
                }
                int mark = json.Length;
                if (!first)
                {
                    json.Append(",");
                }
                json.Append(Quote(field.Name)).Append(":");
                if (WriteValue(json, value, depth + 1))
                {
                    first = false;
                }
                else
                {
                    json.Length = mark;
                }
            }
        }

        /// <summary>Returns false (and writes nothing useful) for default or empty values.</summary>
        private static bool WriteValue(StringBuilder json, object value, int depth)
        {
            if (value == null)
            {
                return false;
            }
            var text = value as string;
            if (text != null)
            {
                if (text.Length == 0)
                {
                    return false;
                }
                json.Append(Quote(text));
                return true;
            }
            Type type = value.GetType();
            if (type == typeof(bool))
            {
                if (!(bool)value)
                {
                    return false;
                }
                json.Append("true");
                return true;
            }
            if (type.IsEnum)
            {
                json.Append(Quote(value.ToString()));
                return true;
            }
            if (type.IsPrimitive)
            {
                if (type == typeof(char))
                {
                    json.Append(Quote(value.ToString()));
                    return true;
                }
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (number == 0.0)
                {
                    return false;
                }
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    json.Append(Quote(number.ToString(CultureInfo.InvariantCulture)));
                    return true;
                }
                json.Append(((IFormattable)value).ToString(type == typeof(float) || type == typeof(double) ? "R" : null,
                    CultureInfo.InvariantCulture));
                return true;
            }
            var unityObject = value as UnityEngine.Object;
            if (!ReferenceEquals(unityObject, null))
            {
                if (unityObject == null)
                {
                    return false;
                }
                if (!(unityObject is ScriptableObject))
                {
                    json.Append(Quote("@" + type.Name + ":" + unityObject.name));
                    return true;
                }
                json.Append("{\"$ref\":").Append(Quote(unityObject.name));
                json.Append(",\"$type\":").Append(Quote(type.Name));
                var unique = unityObject as UniqueIDScriptable;
                if (unique != null)
                {
                    json.Append(",\"$id\":").Append(Quote(unique.UniqueID));
                }
                json.Append("}");
                return true;
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }
            var list = value as IList;
            if (list != null)
            {
                if (list.Count == 0)
                {
                    return false;
                }
                json.Append("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                    {
                        json.Append(",");
                    }
                    if (depth >= MaxDepth || !WriteValue(json, list[i], depth + 1))
                    {
                        // Keep positions: arrays are often index-matched in this game.
                        json.Append("null");
                    }
                }
                json.Append("]");
                return true;
            }
            if (value is IDictionary || depth >= MaxDepth)
            {
                return false;
            }
            bool gameType = type.Assembly == GameAssembly;
            bool unityStruct = type.IsValueType && type.Namespace != null && type.Namespace.StartsWith("UnityEngine");
            if (!gameType && !unityStruct)
            {
                return false;
            }
            int start = json.Length;
            json.Append("{");
            int afterBrace = json.Length;
            WriteFields(json, value, depth, true);
            if (json.Length == afterBrace)
            {
                json.Length = start;
                return false;
            }
            json.Append("}");
            return true;
        }

        private static FieldInfo[] SerializedFields(Type type)
        {
            FieldInfo[] cached;
            if (FieldCache.TryGetValue(type, out cached))
            {
                return cached;
            }
            var fields = new List<FieldInfo>();
            var chain = new List<Type>();
            for (Type current = type; current != null && current != typeof(object) &&
                 current != typeof(ScriptableObject) && current != typeof(UnityEngine.Object); current = current.BaseType)
            {
                chain.Insert(0, current);
            }
            foreach (Type current in chain)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                              BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsNotSerialized)
                    {
                        continue;
                    }
                    bool unitySerialized = field.IsDefined(typeof(SerializeField), true);
                    bool unityStruct = type.IsValueType && type.Namespace != null && type.Namespace.StartsWith("UnityEngine");
                    if (field.IsPublic || unitySerialized || unityStruct)
                    {
                        fields.Add(field);
                    }
                }
            }
            cached = fields.ToArray();
            FieldCache[type] = cached;
            return cached;
        }

        private static string Quote(string text)
        {
            var result = new StringBuilder(text.Length + 2);
            result.Append('"');
            foreach (char letter in text)
            {
                switch (letter)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (letter < ' ')
                        {
                            result.Append("\\u").Append(((int)letter).ToString("x4"));
                        }
                        else
                        {
                            result.Append(letter);
                        }
                        break;
                }
            }
            result.Append('"');
            return result.ToString();
        }
    }
}
