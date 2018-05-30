
<br />
<p align="center">
    <img src="/Images/OdinSerializerLogo.png" alt="Odin Serializer">
</p>
<h3 align="center" style="text-align:center;">
Fast, robust, powerful and extendible .NET serializer built for Unity
</h3>
<p align="center">
OdinSerializer is an open-source version of the custom serializer built for and used by <a href="https://www.assetstore.unity3d.com/en/#!/content/89041">Odin - Inspector & Serializer</a>
</p>
<hr>

<p align="center">
    <a href="https://discord.gg/AgDmStu">Join us on Discord</a>
    <a href="https://www.assetstore.unity3d.com/en/">Get it from the Asset Store</a>
    <a href="https://www.assetstore.unity3d.com/#!/content/89041?aid=1011l36zv">Inspect all serialized data with Odin Inspector</a>
</p>

# How to get started

This section is currently under construction...

#### Using OdinSerializer out of the box

#### Forking OdinSerializer

# Performance charts and comparisons

|                                       | Odin Serializer  | Unity JSON       | Full Serializer  | Binary Formatter | JSON .Net        |Protobuf          |
|---------------------------------------|------------------|------------------|------------------|------------------|------------------|------------------|
|Open Source                            |:heavy_check_mark:|:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|
|Cross Platform                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Out of the box Unity Support           |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:x:               |:x:
|Supports Unity structs                 |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:x:               |?
|Prefab Modifications                   |:heavy_check_mark:|:x:               |:x:               |:x:               |:x:               |:x:
|Binary Formatter                       |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:x:               |:heavy_check_mark:
|Json Formatter                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:heavy_check_mark:|:x:
|Merge friendly data in Unity objects   |:heavy_check_mark:|:x:               |:x:               |:x:               |:x:               |:x:
|Interfaces                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Properties                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:
|Polymorphism                           |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Generics                               |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Dictionaries                           |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Circular  References                   |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Delegates                              |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:x:               |:x:
|Multi-dimensional arrays               |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:heavy_check_mark:|:x:
|Extendable                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Renaming Members                       |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Renaming Types                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|IL Optimized                           |:heavy_check_mark:|-                 |:x:               |:x:               |:x:               |-
|Supports .Net interfaces               |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|?                 |:x:
|Supports .Net callback attributes      |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:

#### Serialization of a simple object with no polymorphism
![Benchmark](/Images/SimpleObjectSerializationBenchmark.png)
#### Serialization of a comple object with lots of polymorphism
![Benchmark](/Images/ComplexObjectSerializationBenchmark.png)
#### Serialization of various large arrays and lists
![Benchmark](/Images/HugeArraysSerializationBenchmark.png)
#### Garbage collection of the 3 tests above
![Benchmark](/Images/GarbageCollectionSerializationBenchmark.png)

## How to contribute

We are taking contributions under the Apache 2.0 license, so please feel free to submit pull requests. Please keep in mind the following rules when submitting contributions:

* Follow the pre-existing coding style and standards in the OdinSerializer code.
* If you work in your own fork with a modified OdinSerializer namespace, please ensure that the pull request uses the correct namespaces for this repository and otherwise compiles right away, so we don't need to clean that sort of stuff up when accepting the pull request.

We are taking any contributions that add value to OdinSerializer without also adding undue bloat or feature creep. However, these are the areas that we are particularly interested in seeing contributions in:

#### Bugfixes
* We would be very grateful if you could submit pull requests for any bugs that you encounter and fix.

#### Performance
* General overall performance: faster code is always better, as long as the increase in speed does not sacrifice any robustness.
* Json format performance: the performance of the json format (JsonDataWriter/JsonDataReader) is currently indescribably awful. The format was originally written as a testbed format for use during the development of OdinSerializer, since it is human-readable and thus very useful for debugging purposes, and has remained largely untouched since.
* EnumSerializer currently allocates garbage via boxing serialized and deserialized enum values. Any approaches for fixing this would be most welcome. Some unsafe code may be required, but we haven't yet had time to really look into this properly.

#### Testing
* A thorough set of standalone unit tests. Odin Inspector has its own internal integration tests for OdinSerializer, but currently we have no decent stand-alone unit tests that solely work with OdinSerializer.

## Technical Overview

The following section is a brief technical overview of the working principles of OdinSerializer and many of its bigger features. First, however, let's have a super brief overview, to give some context before we begin.

This is how OdinSerializer works, on the highest level:

* Data to be written to or read from is passed to a data writer/reader, usually in the form of a stream.
* The data writer/reader is passed to a serializer, along with a value to be serialized if it's serialization happening.
* If the value can be treated as an atomic primitive, the serializer will write or read that directly using the passed data writer/reader.
* If the value is "complex", IE, it is a value that consists of other values, the serializer will get and use a formatter to read or write the value.

### "Stack-only", forward-only

OdinSerializer is a forward-only serializer, meaning that when it serializes, it writes data immediately as it inspects the object graph, and as it deserializes, it recreates the object graph immediately as it parses the data. The serializer only ever moves forward - it cannot "go back" and look at previous data, since we retain no state and are doing everything on the fly, as we move forward. Unlike some other serializers, there is no "meta-graph" data structure that is allocated containing all the data to be saved down later.

This means that we can serialize and deserialize data entirely without allocating anything on the heap, meaning that after the system has run once and all the writer, reader, formatter and serializer instances have been created, there will often be literally zero superfluous GC allocations made, depending on the data format used.

### Data writers and readers

Data writers and readers are types that implement the IDataReader and IDataWriter interfaces. They abstract the writing and reading of strongly typed C# data in the form of atomic primitives, from the actual raw data format that the data is written into and read from. OdinSerializer currently ships with data readers and writers that support three different formats: Json, Binary and Nodes.

Data writers and readers also contain a serialization or deserialization context, which is used to configure how serialization and deserialization operates in various ways.

### Atomic primitives

An atomic primitive (or merely a primitive), in the context of OdinSerializer, is a type that can be written or read in a single call to a data writer or reader. All other types are considered complex types, and must be handled by a formatter that translates that type into a series of atomic primitives. You can check whether something is an atomic primitive by calling FormatterUtilities.IsPrimitiveType(Type type).

The following types are considered atomic primitives:

* System.Char (char)
* System.String (string)
* System.Boolean (bool)
* System.SByte (sbyte)
* System.Byte (byte)
* System.Short (short)
* System.UShort (ushort)
* System.Int (int)
* System.UInt (uint)
* System.Long (long)
* System.ULong (ulong)
* System.Single (float)
* System.Double (double)
* System.Decimal (decimal)
* System.IntPtr
* System.UIntPtr
* System.Guid
* All enums

### Serializers and formatters

This is an important distinction - serializers are the outward "face" of the system, and are all hardcoded into the system. There is a hardcoded serializer type for each atomic primitive, and a catch-all ComplexTypeSerializer that handles all other types by wrapping the use of formatters.

Formatters are what translates an actual C# object into the data that it consists of. They are the primary point of extension in OdinSerializer - they tell the system how to treat various special types. For example, there is a formatter that handles arrays, a formatter that handles multi-dimensional arrays, a formatter that handles dictionaries, and so on.

OdinSerializer ships with a large number of custom formatters for commonly serialized .NET and Unity types. An example of a custom formatter might be the following formatter for Unity's Vector3 type:

```csharp
using OdinSerializer;
using UnityEngine;

[assembly: RegisterFormatter(typeof(Vector3Formatter))]
	
public class Vector3Formatter : MinimalBaseFormatter<Vector3>
{
	private static readonly Serializer<float> FloatSerializer = Serializer.Get<float>();

	protected override void Read(ref Vector3 value, IDataReader reader)
	{
		value.x = FloatSerializer.ReadValue(reader);
		value.y = FloatSerializer.ReadValue(reader);
		value.z = FloatSerializer.ReadValue(reader);
	}
	
	protected override void Write(ref Vector3 value, IDataWriter writer)
	{
		FloatSerializer.WriteValue(value.x, writer);
		FloatSerializer.WriteValue(value.y, writer);
		FloatSerializer.WriteValue(value.z, writer);
	}
}
```

All complex types that do not have a custom formatter declared are serialized using either an on-demand emitted formatter, or if emitted formatters are not available on the current platform, using a fallback reflection-based formatter. These "default" formatters use the serialization policy set on the current context to decide which members are serialized.

### Serialization policies

Serialization policies are used by non-custom formatters (emitted formatters and the reflection formatter) to decide which members should and should not be serialized. A set of default policies are provided in the SerializationPolicies class.

### External references

External references are a very useful concept to have. In short, it's a way for serialized data to refer to objects that are not stored in the serialized data itself, but should instead be fetched externally upon deserialization.

For example, if an object graph refers to a very large asset such as a texture, stored in a central asset database, you might not want the entire texture to be serialized down along with the graph, but might instead want to serialize an external reference to the texture, which would then be resolved again upon deserialization. When working with Unity, this feature is particularly useful, as you will see in the next section.

External references in OdinSerializer can be either by index (int), by guid (System.Guid), or by string, and are used by implementing the IExternalIndexReferenceResolver, IExternalGuidReferenceResolver or IExternalStringReferenceResolver interfaces, and setting a resolver instance on the context that is set on the current data reader or writer.

All reference types (except strings, which are treated as atomic primitives) can potentially be resolved externally, and all available external reference resolvers in the current context will be queried for each encountered reference type object as to whether or not that object ought to be serialized as an external reference.

### How OdinSerializer works in Unity

OdinSerializer comes with a built-in Unity integration for use with types derived from Unity's special UnityEngine.Object class. This integration comes primarily in the form of the UnitySerializationUtility class, and the various convenience classes to derive from, that implement OdinSerializer using UnitySerializationUtility. Each such class derives from a given commonly used UnityEngine.Object type:

* SerializedUnityObject : UnityEngine.Object
* SerializedScriptableObject : UnityEngine.ScriptableObject
* SerializedComponent : UnityEngine.Component
* SerializedBehaviour : UnityEngine.Behaviour
* SerializedMonoBehaviour : UnityEngine.MonoBehaviour
* SerializedNetworkBehaviour : UnityEngine.NetworkBehaviour
* SerializedStateMachineBehaviour : UnityEngine.StateMachineBehaviour

Deriving from any of these types means that your derived type will be serialized using UnitySerializationUtility. Note that this will, by default, *not* serialize *all* serialized members on your derived type. These convenience types use the SerializationPolicies.Unity policy to select members for serialization, and also have the added behaviour that *they will not serialize any members on the derived type that Unity would usually serialize.* Note that this only applies directly on the root members of the serialized UnityEngine.Object-derived type. Take the following example:

```csharp
// This is the component you put on a GameObject. It is THIS component, and only this component, that decides whether or not something is serialized by Odin
public class MyMonoBehaviour : SerializedMonoBehaviour // Inheriting the component from SerializedMonoBehaviour means we use Odin's serialization
{
    public Dictionary<string, string> someDictionary; // Will be serialized by Odin

    [SerializeField]
    private SomeClass someClass1; // Will be serialized by Unity, NOT Odin. "someClass1.someString" will be serialized, but "someClass1.someDict" will NOT be serialized. Polymorphism is NOT supported. Null values are NOT supported.

    [OdinSerialize]
    private SomeClass someClass2; // Will be serialized by Odin, NOT Unity. Both "someClass2.someString" and "someClass2.someDict" will be serialized. Both polymorphism and null values are supported.
}

[Serializable]
public class SomeClass // No matter what you inherit from here, it makes no difference at all to the serialization of this class - that is decided "higher up" in the component itself
{
    public string someString;
    public Dictionary<string, string> someDict;
}
```

If you wish to change this behaviour, you must implement your own special serialized UnityEngine.Object type using Unity's ISerializationCallbackReceiver interface, and manually change either the policy or the arguments passed to UnitySerializationUtility. For example:

```csharp
using UnityEngine;
using OdinSerializer;
using OdinSerializer.Utilities;

public class YourMonoBehaviour : MonoBehaviour, ISerializationCallbackReceiver
{
	[SerializeField, HideInInspector]
	private SerializationData serializationData;

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		using (var cachedContext = Cache<DeserializationContext>.Claim())
		{
			cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
			UnitySerializationUtility.DeserializeUnityObject(this, ref this.serializationData, cachedContext.Value);
		}
	}

	void ISerializationCallbackReceiver.OnBeforeSerialize()
	{
		using (var cachedContext = Cache<SerializationContext>.Claim())
		{
			cachedContext.Value.Config.SerializationPolicy = SerializationPolicies.Everything;
			UnitySerializationUtility.SerializeUnityObject(this, ref this.serializationData, serializeUnityFields: true, context: cachedContext.Value);
		}
	}
}
```

Finally, it should be noted that the UnitySerializationUtility class always sets a UnityReferenceResolver as an external index resolver. This external reference resolver ensures that all references to Unity objects encountered in the data to serialize become external references, which are shunted into a list in the SerializationData struct for Unity to serialize, and later used to link the external references back up to the correct Unity instance.

This is done because there is no way for Odin to actually serialize and later reconstruct most UnityEngine.Object-derived types, and therefore, we have established it as a hard rule that we will never even try to do so.

### AOT Support details

OdinSerializer contains a utility class, AOTSupportUtilities, for providing support for AOT (Ahead-Of-Time) compiled platforms such as IL2CPP and Mono AOT. This utility can scan the entire project and generate a list of types that are serialized by OdinSerializer, and it can take a list of types and create a .dll file in your project that ensures that there will be serialization support for all the given types in an AOT build.

To automate this process of AOT support, you can use Unity's IPreProcessBuild/IPreProcessBuildWithReport and IPostProcessBuild/IPostProcessBuildWithReport interfaces to create an AOT support dll upon building, and delete it again after building. (Note that this only becomes possible in Unity 5.6, where IPreProcessBuild was introduced.)

The following code example does just that, though it currently creates an AOT dll for *all* platforms, not merely AOT platforms. Please feel free to modify it to suit your own needs:

```csharp
#if UNITY_EDITOR
using OdinSerializer.Editor;
using UnityEditor;
using UnityEditor.Build;
using System.IO;
using System;

#if UNITY_2018_1_OR_NEWER

using UnityEditor.Build.Reporting;

#endif

#if UNITY_2018_1_OR_NEWER
public class PreBuildAOTAutomation : IPreprocessBuildWithReport
#else
public class PreBuildAOTAutomation : IPreprocessBuild
#endif
{
	public int callbackOrder
	{
		get
		{
			return -1000;
		}
	}

	public void OnPreprocessBuild(BuildTarget target, string path)
	{
		// Create AOT support dll
		List<Type> types;
		if (AOTSupportUtilities.ScanProjectForSerializedTypes(out types))
		{
			AOTSupportUtilities.GenerateDLL("Assets/OdinAOTSupport", "MyAOTSupportDll", types);
		}
	}

#if UNITY_2018_1_OR_NEWER

	public void OnPreprocessBuild(BuildReport report)
	{
		this.OnPreprocessBuild(report.summary.platform, report.summary.outputPath);
	}

#endif
}

#if UNITY_2018_1_OR_NEWER
public class PostBuildAOTAutomation : IPostprocessBuildWithReport
#else
public class PostBuildAOTAutomation : IPostprocessBuild
#endif
{
	public int callbackOrder
	{
		get
		{
			return -1000;
		}
	}

	public void OnPostprocessBuild(BuildTarget target, string path)
	{
		// Delete AOT support dll after build so it doesn't pollute the project
		Directory.Delete("Assets/OdinAOTSupport", true);
		File.Delete("Assets/OdinAOTSupport.meta");
		AssetDatabase.Refresh();
	}

#if UNITY_2018_1_OR_NEWER

	public void OnPostprocessBuild(BuildReport report)
	{
		this.OnPostprocessBuild(report.summary.platform, report.summary.outputPath);
	}

#endif
}
#endif
```

