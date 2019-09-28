<br />
<p align="center">
    <img src="/Images/OdinSerializerLogo.png" alt="Odin Serializer">
</p>
<h3 align="center" style="text-align:center;">
	Fast, robust, powerful and extendible .NET serializer built for Unity
</h3>
<p align="center">
	OdinSerializer is an open-source version of the custom serializer built for and used by 
	<a href="https://odininspector.com/">Odin - Inspector & Serializer</a>
</p>
<hr>
<p align="center">	
	<a href="https://twitter.com/TeamSirenix">
		<img src="/Images/BtnTwitter.png" alt="Sirenix Twitter">
	</a>
	<a href="https://discord.gg/AgDmStu">
		<img src="https://discordapp.com/api/guilds/355444042009673728/embed.png" alt="Discord server">
	</a>
	<a href="https://odininspector.com/">
		<img src="/Images/BtnOdinInspector.png" alt="Inspect all data with Odin Serializer">
	</a>
	<a href="https://odininspector.com/download">
		<img src="/Images/BtnDownload.png" alt="Download">
	</a>
	<a href="https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=YFY8ZF347Z2PN">
		<img src="/Images/BtnDonate.png" alt="Donate">
	</a>	
</p>
<hr>
<h3 align="center" style="text-align:center;">
	Used in hundreds of games and supported by Asset Store developers such as
</h3>

<p align="center">	
	<a href="http://www.devdog.io" target="_blank">
		<img src="/Images/DevdogLogo.png" alt="DevDog">
	</a>
	<a href="https://assetstore.unity.com/publishers/735" target="_blank">
		<img src="/Images/ParadoxNotionLogo.png" alt="ParadoxNotion">
	</a>
	<a href="https://assetstore.unity.com/publishers/11548" target="_blank">
		<img src="/Images/LudiqLogo.png" alt="Ludiq">
	</a>
</p>
<hr>

## Performance charts and comparisons

OdinSerializer compares very well to many popular serialization libraries in terms of performance and garbage allocation, while providing a superior feature-set for use in Unity.

The performance graphs in this section are profiled with OdinSerializer's binary format.

|                                       | Odin Serializer  | Unity JSON       | Full Serializer  | Binary Formatter | JSON.NET        |Protobuf-net          |
|---------------------------------------|------------------|------------------|------------------|------------------|------------------|------------------|
|Open Source                            |:heavy_check_mark:|:x:|:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|
|Cross Platform                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Out of the box Unity Support           |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:x:               |:x:
|Supports Unity structs                 |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:x:               |:x:
|Binary Formatter                       |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:x:               |:heavy_check_mark:
|Json Formatter                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:x:               |:heavy_check_mark:|:x:
|Merge-friendly data in Unity objects   |:heavy_check_mark:|:x:               |:x:               |:x:               |:x:               |:x:
|Interfaces                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Properties                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:
|Polymorphism                           |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Generics                               |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Dictionaries                           |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Circular References                   |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Delegates                              |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:x:               |:x:
|Multi-dimensional arrays               |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:heavy_check_mark:|:x:
|Extendable                             |:heavy_check_mark:|:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Renaming Members                       |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|Renaming Types                         |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:
|IL Optimized                           |:heavy_check_mark:|-                 |:x:               |:x:               |:x:               |-
|Supports .NET interfaces               |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|?                 |:x:
|Supports .NET callback attributes      |:heavy_check_mark:|:x:               |:x:               |:heavy_check_mark:|:heavy_check_mark:|:heavy_check_mark:

#### Serialization of a simple object with no polymorphism
![Benchmark](/Images/SimpleObjectSerializationBenchmark.png)
#### Serialization of a complex object with lots of polymorphism
##### *Unity JsonUtility has been excluded from this benchmark because it supports neither polymorphism or dictionaries
![Benchmark](/Images/ComplexObjectSerializationBenchmark.png)
#### Serialization of various large arrays and lists
![Benchmark](/Images/HugeArraysSerializationBenchmark.png)
#### Garbage allocation of the 3 tests above
![Benchmark](/Images/GarbageCollectionSerializationBenchmark.png)

## How to get started

There are many different use cases for OdinSerializer. If you just need a serialization library to use in your own private project, we can recommend that you simply use it out of the box. If you would like to make your own tweaks and builds, or if you intend to include OdinSerializer in a package that you are distributing, you would be better served by forking the repository.

### Using OdinSerializer out of the box

To use OdinSerializer as-is, simply head to the [download](https://odininspector.com/download) page on our website to download the latest commit as a .unitypackage file, and import the package into your Unity project. OdinSerializer will then be in your project, ready to use.

### Forking OdinSerializer

*Note: currently, working with and building the OdinSerializer project has only been tested on Windows machines, using Visual Studio.*

To get started, you may want to read [GitHub's guide to forking](https://guides.github.com/activities/forking/), to get the basics of forking down. 

Once you've forked OdinSerializer, you can start making your own changes to the project. Perhaps you want to add a feature, or tweak a part of it to suit your own needs better.

If you intend to include OdinSerializer in one of your own product distributions, you should modify all source files using a tool like search and replace to move the OdinSerializer namespace into an appropriate namespace for your project, and rename the .dll's that are built. This is to avoid namespace and assembly conflicts in the cases where multiple different assets in the same project all use possibly differing versions of OdinSerializer. For example, you might globally rename "OdinSerializer" to "MyProject.Internal.OdinSerializer", and also have the .dll's renamed to "MyProject.Internal.OdinSerializer.dll".

Here's a goto list of things that need to be renamed during this process:

- OdinSerializer.csproj: AssemblyName and RootNamespace and XML doc path.
- OdinBuildAutomation.cs: Namespace, and assembly name strings in the static constructor.
- Namespaces in the entire OdinSerializer project (search and replace is your friend).
- The link.xml file included in the AOT folder.

### Building OdinSerializer

The OdinSerializer project is set up as an independent code project that lives outside of Unity, and which can compile assemblies for use inside of a Unity project. Its build settings are set up to use a specific MSBuild distributable (Roslyn compiler) to build assemblies that are Unity-compatible, and the pdb2mdb tool to convert .pdb symbol files to .mdb symbol files to support proper step debugging in Unity. Simply building with the default MSBuild versions that ship with many recent distributions of Visual Studio appears to cause instant runtime crashes in some versions of Unity the moment the code enters an unsafe context.

The easiest way to build and test OdinSerializer, however, is to open the Build folder as a Unity project (any Unity version at or above 5.3 should work). When open, you will see three buttons in the project scene view: "Compile with debugging", "Compile release build" and "Open solution".

*Compile with debugging* compiles an assembly into the open project, along with the proper symbol files for step debugging. Once this assembly is imported by Unity, you can step debug OdinSerializer by attaching a Visual Studio instance to it.

*Compile release build* compiles three different OdinSerializer assembly variants with release optimizations. The three assemblies are for use in respectively the editor, in builds with JIT support (Windows/Mac/Linux), and in AOT builds (IL2CPP). The OdinSerializer Unity project also includes one script file, OdinSerializerBuildAutomation.cs, which automates setting the import settings of these three assemblies correctly during the build process based on the current target platform and scripting backend so that the correct assembly is used for that build, as well as automatically scanning the project and generating AOT support for types that need it, if the build is AOT compiled. We encourage you to modify this file or otherwise adapt it to suit your own particular automation needs.

*Open solution* simply opens the OdinSerializer solution file using the default application.

### Basic usage of OdinSerializer

This section will not go into great detail about how OdinSerializer works or how to configure it in advanced ways - for that, see the technical overview further down. Instead, it aims to give a simple overview of how to use OdinSerializer in a basic capacity.

There are, broadly, two different ways of using OdinSerializer:

#### Serializing regular C# objects

You can use OdinSerializer as a standalone serialization library, simply serializing or deserializing whatever data you give it, for example to be stored in a file or sent over the network. This is done using the SerializationUtility class, which contains a variety of methods that wrap OdinSerializer for straight-forward, easy use.

###### Example: Serializing regular C# objects

```csharp
using OdinSerializer;

public static class Example
{
	public static void Save(MyData data, string filePath)
	{
		byte[] bytes = SerializationUtility.SerializeValue(data, DataFormat.Binary);
		File.WriteAllBytes(bytes, filePath);
	}
	
	public static MyData Load(string filePath)
	{
		byte[] bytes = File.ReadAllBytes(filePath);
		return SerializationUtility.DeserializeValue<MyData>(bytes, DataFormat.Binary);
	}
}
```

Note that you cannot save references to Unity objects or assets down to a file in this manner. The only way to handle this is to ask for a list of all encountered UnityEngine.Object references when serializing, and then pass that list of references back into the system when deserializing data.

###### Example: Serializing regular C# objects containing Unity references

```csharp
using OdinSerializer;

public static class Example
{
	public static void Save(MyData data,  string filePath, ref List<UnityEngine.Object> unityReferences)
	{
		byte[] bytes = SerializationUtility.SerializeValue(data, DataFormat.Binary, out unityReferences);
		File.WriteAllBytes(bytes, filePath);
		
		// The unityReferences list will now be filled with all encountered UnityEngine.Object references, and the saved binary data contains index pointers into this list.
		// It is your job to ensure that the list of references stays the same between serialization and deserialization.
	}
	
	public static MyData Load(string filePath, List<UnityEngine.Object> unityReferences)
	{
		byte[] bytes = File.ReadAllBytes(filePath);
		return SerializationUtility.DeserializeValue<MyData>(bytes, DataFormat.Binary, unityReferences);
	}
}
```

#### Extending UnityEngine.Object serialization

You can also use OdinSerializer to seamlessly extend the serialization of Unity's object types, such as ScriptableObject and MonoBehaviour. There are two general ways of doing this, one of which is manual and requires a few lines of code to implement, and one of which is very easy to implement, but exhibits only the default behaviour.

The easier approach is to simply derive your type from one of many pre-created UnityEngine.Object-derived types that OdinSerializer provides, that have the above behaviour implemented already. Note that doing this will have the default behaviour of not serializing fields that Unity will serialize.

###### Example: Easily extending UnityEngine.Object serialization

```csharp
using OdinSerializer;

public class YourSpeciallySerializedScriptableObject : SerializedScriptableObject
{
	public Dictionary<string, string> iAmSerializedByOdin;
	public List<string> iAmSerializedByUnity;
}
```

The manual method requires that you implement Unity's ISerializationCallbackReceiver interface on the UnityEngine.Object-derived type you want to extend the serialization of, and then use OdinSerializer's UnitySerializationUtility class to apply Odin's serialization during the serialization callbacks that Unity invokes at the appropriate times.

###### Example: Manually extending UnityEngine.Object serialization

```csharp
using UnityEngine;
using OdinSerializer;

public class YourSpeciallySerializedScriptableObject : ScriptableObject, ISerializationCallbackReceiver
{
	[SerializeField, HideInInspector]
	private SerializationData serializationData;

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		// Make Odin deserialize the serializationData field's contents into this instance.
		UnitySerializationUtility.DeserializeUnityObject(this, ref this.serializationData, cachedContext.Value);
	}

	void ISerializationCallbackReceiver.OnBeforeSerialize()
	{
		// Whether to always serialize fields that Unity will also serialize. By default, this parameter is false, and OdinSerializer will only serialize fields that it thinks Unity will not handle.
		bool serializeUnityFields = false;
		
		// Make Odin serialize data from this instance into the serializationData field.
		UnitySerializationUtility.SerializeUnityObject(this, ref this.serializationData, serializeUnityFields, cachedContext.Value);
	}
}
```

NOTE: If you use OdinSerializer to extend the serialization of a Unity object, without having an inspector framework such as Odin Inspector installed, the Odin-serialized fields will not be rendered properly in Unity's inspector. You will either have to acquire such a framework, or write your own custom editor to be able to inspect and edit this data in Unity's inspector window.

Additionally, always remember that Unity doesn't strictly know that this extra serialized data exists - whenever you change it from your custom editor, remember to manually mark the relevant asset or scene dirty, so Unity knows that it needs to be re-serialized.

Finally, note that *prefab modifications will not simply work by default in specially serialized Components/Behaviours/MonoBehaviours*. Specially serialized prefab instances may explode and die if you attempt to change their custom-serialized data from the parent prefab. OdinSerializer contains a system for managing an object's specially-serialized prefab modifications and applying them, but this is an advanced use of OdinSerializer that requires heavy support from a specialised custom editor, and this is not covered in this readme.

## How to contribute

We are taking contributions under the Apache 2.0 license, so please feel free to submit pull requests. Please keep in mind the following rules when submitting contributions:

* Follow the pre-existing coding style and standards in the OdinSerializer code.
* If you work in your own fork with a modified OdinSerializer namespace, please ensure that the pull request uses the correct namespaces for this repository and otherwise compiles right away, so we don't need to clean that sort of stuff up when accepting the pull request.

We are taking any contributions that add value to OdinSerializer without also adding undue bloat or feature creep. However, these are the areas that we are particularly interested in seeing contributions in:

#### Bugfixes
* We would be very grateful if you could submit pull requests for any bugs that you encounter and fix.

#### Performance
* General overall performance: faster code is always better, as long as the increase in speed does not sacrifice any robustness.
* Json format performance: the performance of the json format (JsonDataWriter/JsonDataReader) is currently indescribably awful. The format was originally written as a testbed for use during the development of OdinSerializer, since it is human-readable and thus very useful for debugging purposes, and has remained largely untouched since.
* EnumSerializer currently allocates garbage via boxing serialized and deserialized enum values. As such, serializing enums always results in unnecessary garbage being allocated. Any approaches for fixing this would be most welcome. Some unsafe code may be required, but we haven't yet had time to really look into this properly.

#### Testing
* A thorough set of standalone unit tests. Odin Inspector has its own internal integration tests for OdinSerializer, but currently we have no decent stand-alone unit tests that work solely with OdinSerializer.

## Technical Overview

The following section is a brief technical overview of the working principles of OdinSerializer and many of its bigger features. First, however, let's have a super brief overview, to give some context before we begin.

This is how OdinSerializer works, on the highest level:

* Data to be written to or read from is passed to a data writer/reader, usually in the form of a stream.
* The data writer/reader is passed to a serializer, along with a value to be serialized, if we are serializing.
* If the value can be treated as an atomic primitive, the serializer will write or read that directly using the passed data writer/reader. If the value is "complex", IE, it is a value that consists of other values, the serializer will get and wrap the use of a formatter to read or write the value.

### "Stack-only", forward-only

OdinSerializer is a forward-only serializer, meaning that when it serializes, it writes data immediately as it inspects the object graph, and when it deserializes, it recreates the object graph immediately as it parses the data. The serializer only ever moves forward - it cannot "go back" and look at previous data, since we retain no state and are doing everything on the fly, as we move forward. Unlike some other serializers, there is no "meta-graph" data structure that is allocated containing all the data to be saved down later.

This means that we can serialize and deserialize data entirely without allocating anything on the heap, meaning that after the system has run once and all the writer, reader, formatter and serializer instances have been created, there will often be literally zero superfluous GC allocations made, depending on the data format used.

### Data writers and readers

Data writers and readers are types that implement the IDataReader and/or IDataWriter interfaces. They abstract the writing and reading of strongly typed C# data in the form of atomic primitives, from the actual raw data format that the data is written into and read from. OdinSerializer currently ships with data readers and writers that support three different formats: Json, Binary and Nodes.

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

Formatters are what translates an actual C# object into the primitive data that it consists of. They are the primary point of extension in OdinSerializer - they tell the system how to treat various special types. For example, there is a formatter that handles arrays, a formatter that handles multi-dimensional arrays, a formatter that handles dictionaries, and so on.

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

OdinSerializer contains two utility classes, AOTSupportUtilities and AOTSupportScanner, for providing support for AOT (Ahead-Of-Time) compiled platforms such as IL2CPP and Mono AOT. These utilities can be used to scan the entire project (or merely parts of it) and generate a list of types that are serialized by OdinSerializer, and they can take a list of types and create a .dll file in your project that ensures that there will be serialization support for all the given types in an AOT build.

To automate this process of AOT support, you can use Unity's IPreProcessBuild/IPreProcessBuildWithReport and IPostProcessBuild/IPostProcessBuildWithReport interfaces to create an AOT support dll upon building, and delete it again after building. (Note that this only becomes possible in Unity 5.6, where IPreProcessBuild was introduced.)

OdinSerializer already includes one script file in the Unity project, OdinSerializerBuildAutomation.cs, which automates setting the import settings of OdinSerializer's three assembly variants correctly during the build process based on the current target platform and scripting backend, as well as automatically scanning the project and generating AOT support for types that need it, if the build is AOT compiled. We encourage you to modify this file to suit your own particular automation needs.
