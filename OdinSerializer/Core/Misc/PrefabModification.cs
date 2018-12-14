//-----------------------------------------------------------------------
// <copyright file="PrefabModification.cs" company="Sirenix IVS">
// Copyright (c) 2018 Sirenix IVS
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//-----------------------------------------------------------------------
namespace OdinSerializer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Utilities;

    /// <summary>
    /// An Odin-serialized prefab modification, containing all the information necessary to apply the modification.
    /// </summary>
    public sealed class PrefabModification
    {
        /// <summary>
        /// The type of modification to be made.
        /// </summary>
        public PrefabModificationType ModificationType;

        /// <summary>
        /// The deep reflection path at which to make the modification.
        /// </summary>
        public string Path;

        /// <summary>
        /// A list of all deep reflection paths in the target object where the value referenced by this modification was also located.
        /// </summary>
        public List<string> ReferencePaths;

        /// <summary>
        /// The modified value to set.
        /// </summary>
        public object ModifiedValue;

        /// <summary>
        /// The new list length to set.
        /// </summary>
        public int NewLength;

        /// <summary>
        /// The dictionary keys to add.
        /// </summary>
        public object[] DictionaryKeysAdded;

        /// <summary>
        /// The dictionary keys to remove.
        /// </summary>
        public object[] DictionaryKeysRemoved;

        /// <summary>
        /// Applies the modification to the given Object.
        /// </summary>
        public void Apply(UnityEngine.Object unityObject)
        {
            if (this.ModificationType == PrefabModificationType.Value)
            {
                this.ApplyValue(unityObject);
            }
            else if (this.ModificationType == PrefabModificationType.ListLength)
            {
                this.ApplyListLength(unityObject);
            }
            else if (this.ModificationType == PrefabModificationType.Dictionary)
            {
                this.ApplyDictionaryModifications(unityObject);
            }
            else
            {
                throw new NotImplementedException(this.ModificationType.ToString());
            }
        }

        private void ApplyValue(UnityEngine.Object unityObject)
        {
            Type valueType = null;

            if (!object.ReferenceEquals(this.ModifiedValue, null))
            {
                valueType = this.ModifiedValue.GetType();
            }

            if (valueType != null && this.ReferencePaths != null && this.ReferencePaths.Count > 0)
            {
                for (int i = 0; i < this.ReferencePaths.Count; i++)
                {
                    var path = this.ReferencePaths[i];

                    try
                    {
                        var refValue = GetInstanceFromPath(path, unityObject);

                        if (!object.ReferenceEquals(refValue, null) && refValue.GetType() == valueType)
                        {
                            this.ModifiedValue = refValue;
                            break;
                        }
                    }
                    catch (Exception) { }
                }
            }

            SetInstanceToPath(this.Path, unityObject, this.ModifiedValue);
        }

        private void ApplyListLength(UnityEngine.Object unityObject)
        {
            object listObj = GetInstanceFromPath(this.Path, unityObject);

            if (listObj == null)
            {
                // The list has been deleted on the prefab;
                // that supersedes our length change.
                return;
            }

            Type listType = listObj.GetType();

            if (listType.IsArray)
            {
                Array array = (Array)listObj;

                if (this.NewLength == array.Length)
                {
                    // If this happens, for some weird reason, then we can actually just not do anything
                    return;
                }

                // We actually need to replace all references to this array in the entire object graph!
                // Ridiculous, we know - but there's no choice...

                // Let's create a new, modified array
                Array newArray = Array.CreateInstance(listType.GetElementType(), this.NewLength);

                if (this.NewLength > array.Length)
                {
                    Array.Copy(array, 0, newArray, 0, array.Length);
                    ReplaceAllReferencesInGraph(unityObject, array, newArray);
                }
                else
                {
                    Array.Copy(array, 0, newArray, 0, newArray.Length);
                    ReplaceAllReferencesInGraph(unityObject, array, newArray);
                }
            }
            else if (typeof(IList).IsAssignableFrom(listType))
            {
                IList list = (IList)listObj;
                Type listElementType = listType.ImplementsOpenGenericInterface(typeof(IList<>)) ? listType.GetArgumentsOfInheritedOpenGenericInterface(typeof(IList<>))[0] : null;
                bool elementIsValueType = listElementType != null ? listElementType.IsValueType : false;

                int count = 0;

                while (list.Count < this.NewLength)
                {
                    if (elementIsValueType)
                    {
                        list.Add(Activator.CreateInstance(listElementType));
                    }
                    else
                    {
                        list.Add(null);
                    }

                    count++;
                }

                while (list.Count > this.NewLength)
                {
                    list.RemoveAt(list.Count - 1);
                }
            }
            else if (listType.ImplementsOpenGenericInterface(typeof(IList<>)))
            {
                Type elementType = listType.GetArgumentsOfInheritedOpenGenericInterface(typeof(IList<>))[0];
                Type collectionType = typeof(ICollection<>).MakeGenericType(elementType);
                bool elementIsValueType = elementType.IsValueType;

                PropertyInfo countProp = collectionType.GetProperty("Count");

                int count = (int)countProp.GetValue(listObj, null);

                if (count < this.NewLength)
                {
                    int add = this.NewLength - count;

                    MethodInfo addMethod = collectionType.GetMethod("Add");

                    for (int i = 0; i < add; i++)
                    {
                        if (elementIsValueType)
                        {
                            addMethod.Invoke(listObj, new object[] { Activator.CreateInstance(elementType) });
                        }
                        else
                        {
                            addMethod.Invoke(listObj, new object[] { null });
                        }
                        count++;
                    }
                }
                else if (count > this.NewLength)
                {
                    int remove = count - this.NewLength;

                    Type listInterfaceType = typeof(IList<>).MakeGenericType(elementType);
                    MethodInfo removeAtMethod = listInterfaceType.GetMethod("RemoveAt");

                    for (int i = 0; i < remove; i++)
                    {
                        removeAtMethod.Invoke(listObj, new object[] { count - (remove + 1) });
                    }
                }
            }
        }

        private void ApplyDictionaryModifications(UnityEngine.Object unityObject)
        {
            object dictionaryObj = GetInstanceFromPath(this.Path, unityObject);

            if (dictionaryObj == null)
            {
                // The dictionary has been deleted on the prefab;
                // that supersedes our dictionary modifications.
                return;
            }

            var type = dictionaryObj.GetType();

            if (!type.ImplementsOpenGenericInterface(typeof(IDictionary<,>)))
            {
                // A value change has changed the target modified value to
                // not be a dictionary - that also supersedes this modification.
                return;
            }

            var typeArgs = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(IDictionary<,>));

            var iType = typeof(IDictionary<,>).MakeGenericType(typeArgs);

            //
            // First, remove keys
            //

            if (this.DictionaryKeysRemoved != null && this.DictionaryKeysRemoved.Length > 0)
            {
                MethodInfo method = iType.GetMethod("Remove", new Type[] { typeArgs[0] });
                object[] parameters = new object[1];

                for (int i = 0; i < this.DictionaryKeysRemoved.Length; i++)
                {
                    parameters[0] = this.DictionaryKeysRemoved[i];

                    // Ensure the key value is safe to add
                    if (object.ReferenceEquals(parameters[0], null) || !typeArgs[0].IsAssignableFrom(parameters[0].GetType()))
                        continue;

                    method.Invoke(dictionaryObj, parameters);
                }
            }

            //
            // Then, add keys
            //

            if (this.DictionaryKeysAdded != null && this.DictionaryKeysAdded.Length > 0)
            {
                MethodInfo method = iType.GetMethod("set_Item", typeArgs);
                object[] parameters = new object[2];

                // Get default value to set key to
                parameters[1] = typeArgs[1].IsValueType ? Activator.CreateInstance(typeArgs[1]) : null;

                for (int i = 0; i < this.DictionaryKeysAdded.Length; i++)
                {
                    parameters[0] = this.DictionaryKeysAdded[i];

                    // Ensure the key value is safe to add
                    if (object.ReferenceEquals(parameters[0], null) || !typeArgs[0].IsAssignableFrom(parameters[0].GetType()))
                        continue;

                    method.Invoke(dictionaryObj, parameters);
                }
            }
        }

        private static void ReplaceAllReferencesInGraph(object graph, object oldReference, object newReference, HashSet<object> processedReferences = null)
        {
            if (processedReferences == null)
            {
                processedReferences = new HashSet<object>(ReferenceEqualityComparer<object>.Default);
            }

            processedReferences.Add(graph);

            if (graph.GetType().IsArray)
            {
                Array array = (Array)graph;

                for (int i = 0; i < array.Length; i++)
                {
                    var value = array.GetValue(i);

                    if (object.ReferenceEquals(value, null))
                    {
                        continue;
                    }

                    if (object.ReferenceEquals(value, oldReference))
                    {
                        array.SetValue(newReference, i);
                        value = newReference;
                    }

                    if (!processedReferences.Contains(value))
                    {
                        ReplaceAllReferencesInGraph(value, oldReference, newReference, processedReferences);
                    }
                }
            }
            else
            {
                var members = FormatterUtilities.GetSerializableMembers(graph.GetType(), SerializationPolicies.Everything);

                for (int i = 0; i < members.Length; i++)
                {
                    FieldInfo field = (FieldInfo)members[i];

                    if (field.FieldType.IsPrimitive || field.FieldType == typeof(SerializationData) || field.FieldType == typeof(string))
                        continue;

                    object value = field.GetValue(graph);

                    if (object.ReferenceEquals(value, null))
                    {
                        continue;
                    }

                    Type valueType = value.GetType();

                    if (valueType.IsPrimitive || valueType == typeof(SerializationData) || valueType == typeof(string))
                        continue;

                    if (object.ReferenceEquals(value, oldReference))
                    {
                        field.SetValue(graph, newReference);
                        value = newReference;
                    }

                    if (!processedReferences.Contains(value))
                    {
                        ReplaceAllReferencesInGraph(value, oldReference, newReference, processedReferences);
                    }
                }
            }
        }

        private static object GetInstanceFromPath(string path, object instance)
        {
            string[] steps = path.Split('.');

            object currentInstance = instance;

            for (int i = 0; i < steps.Length; i++)
            {
                currentInstance = GetInstanceOfStep(steps[i], currentInstance);

                if (object.ReferenceEquals(currentInstance, null))
                {
                    //Debug.LogWarning("Failed to resolve modification path '" + path + "' at step '" + steps[i] + "'.");
                    return null;
                }
            }

            return currentInstance;
        }

        private static object GetInstanceOfStep(string step, object instance)
        {
            Type type = instance.GetType();

            if (step.StartsWith("[", StringComparison.InvariantCulture) && step.EndsWith("]", StringComparison.InvariantCulture))
            {
                int index;
                string indexStr = step.Substring(1, step.Length - 2);

                if (!int.TryParse(indexStr, out index))
                {
                    throw new ArgumentException("Couldn't parse an index from the path step '" + step + "'.");
                }

                // We need to check the current type to see if we can treat it as a list

                if (type.IsArray)
                {
                    Array array = (Array)instance;

                    if (index < 0 || index >= array.Length)
                    {
                        return null;
                    }

                    return array.GetValue(index);
                }
                else if (typeof(IList).IsAssignableFrom(type))
                {
                    IList list = (IList)instance;

                    if (index < 0 || index >= list.Count)
                    {
                        return null;
                    }

                    return list[index];
                }
                else if (type.ImplementsOpenGenericInterface(typeof(IList<>)))
                {
                    Type elementType = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(IList<>))[0];
                    Type listType = typeof(IList<>).MakeGenericType(elementType);
                    MethodInfo getItemMethod = listType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    try
                    {
                        return getItemMethod.Invoke(instance, new object[] { index });
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            else if (step.StartsWith("{", StringComparison.InvariantCultureIgnoreCase) && step.EndsWith("}", StringComparison.InvariantCultureIgnoreCase))
            {
                if (type.ImplementsOpenGenericInterface(typeof(IDictionary<,>)))
                {
                    var dictArgs = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(IDictionary<,>));

                    object key = DictionaryKeyUtility.GetDictionaryKeyValue(step, dictArgs[0]);

                    Type dictType = typeof(IDictionary<,>).MakeGenericType(dictArgs);
                    MethodInfo getItemMethod = dictType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    try
                    {
                        return getItemMethod.Invoke(instance, new object[] { key });
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            else
            {
                string privateTypeName = null;
                int plusIndex = step.IndexOf('+');

                if (plusIndex >= 0)
                {
                    privateTypeName = step.Substring(0, plusIndex);
                    step = step.Substring(plusIndex + 1);
                }

                var possibleMembers = type.GetAllMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(n => n is FieldInfo || n is PropertyInfo);

                foreach (var member in possibleMembers)
                {
                    if (member.Name == step)
                    {
                        if (privateTypeName != null && member.DeclaringType.Name != privateTypeName)
                        {
                            continue;
                        }

                        return member.GetMemberValue(instance);
                    }
                }
            }

            return null;
        }

        private static void SetInstanceToPath(string path, object instance, object value)
        {
            bool setParentInstance;
            string[] steps = path.Split('.');
            SetInstanceToPath(path, steps, 0, instance, value, out setParentInstance);
        }

        private static void SetInstanceToPath(string path, string[] steps, int index, object instance, object value, out bool setParentInstance)
        {
            setParentInstance = false;

            if (index < steps.Length - 1)
            {
                object currentInstance = GetInstanceOfStep(steps[index], instance);

                if (object.ReferenceEquals(currentInstance, null))
                {
                    //Debug.LogWarning("Failed to resolve prefab modification path '" + path + "' at step '" + steps[index] + "'.");
                    return;
                }

                SetInstanceToPath(path, steps, index + 1, currentInstance, value, out setParentInstance);

                if (setParentInstance)
                {
                    // We need to set the current instance to the parent instance member,
                    // because the current instance is a value type, and thus it may have
                    // been boxed. If we don't do this, the value set might be lost.
                    TrySetInstanceOfStep(steps[index], instance, currentInstance, out setParentInstance);
                }
            }
            else
            {
                TrySetInstanceOfStep(steps[index], instance, value, out setParentInstance);
            }
        }

        private static bool TrySetInstanceOfStep(string step, object instance, object value, out bool setParentInstance)
        {
            setParentInstance = false;

            try
            {
                Type type = instance.GetType();

                if (step.StartsWith("[", StringComparison.InvariantCulture) && step.EndsWith("]", StringComparison.InvariantCulture))
                {
                    int index;
                    string indexStr = step.Substring(1, step.Length - 2);

                    if (!int.TryParse(indexStr, out index))
                    {
                        throw new ArgumentException("Couldn't parse an index from the path step '" + step + "'.");
                    }

                    // We need to check the current type to see if we can treat it as a list

                    if (type.IsArray)
                    {
                        Array array = (Array)instance;

                        if (index < 0 || index >= array.Length)
                        {
                            return false;
                        }

                        array.SetValue(value, index);
                        return true;
                    }
                    else if (typeof(IList).IsAssignableFrom(type))
                    {
                        IList list = (IList)instance;

                        if (index < 0 || index >= list.Count)
                        {
                            return false;
                        }

                        list[index] = value;
                        return true;
                    }
                    else if (type.ImplementsOpenGenericInterface(typeof(IList<>)))
                    {
                        Type elementType = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(IList<>))[0];
                        Type listType = typeof(IList<>).MakeGenericType(elementType);
                        MethodInfo setItemMethod = listType.GetMethod("set_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        setItemMethod.Invoke(instance, new object[] { index, value });
                        return true;
                    }
                }
                else if (step.StartsWith("{", StringComparison.InvariantCulture) && step.EndsWith("}", StringComparison.InvariantCulture))
                {
                    if (type.ImplementsOpenGenericInterface(typeof(IDictionary<,>)))
                    {
                        var dictArgs = type.GetArgumentsOfInheritedOpenGenericInterface(typeof(IDictionary<,>));

                        object key = DictionaryKeyUtility.GetDictionaryKeyValue(step, dictArgs[0]);

                        Type dictType = typeof(IDictionary<,>).MakeGenericType(dictArgs);

                        MethodInfo containsKeyMethod = dictType.GetMethod("ContainsKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        MethodInfo setItemMethod = dictType.GetMethod("set_Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        bool containsKey = (bool)containsKeyMethod.Invoke(instance, new object[] { key });

                        if (!containsKey)
                        {
                            // We are *not* allowed to add new keys during this step
                            return false;
                        }

                        setItemMethod.Invoke(instance, new object[] { key, value });
                    }
                }
                else
                {
                    string privateTypeName = null;
                    int plusIndex = step.IndexOf('+');

                    if (plusIndex >= 0)
                    {
                        privateTypeName = step.Substring(0, plusIndex);
                        step = step.Substring(plusIndex + 1);
                    }

                    var possibleMembers = type.GetAllMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(n => n is FieldInfo || n is PropertyInfo);

                    foreach (var member in possibleMembers)
                    {
                        if (member.Name == step)
                        {
                            if (privateTypeName != null && member.DeclaringType.Name != privateTypeName)
                            {
                                continue;
                            }

                            member.SetMemberValue(instance, value);

                            if (instance.GetType().IsValueType)
                            {
                                setParentInstance = true;
                            }

                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}