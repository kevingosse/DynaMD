# DynaMD

## About

Helper objects to browse complex structures returned by [ClrMD](https://www.nuget.org/packages/Microsoft.Diagnostics.Runtime/). This is useful to quickly write scripts to analyze memory dumps.

The library leverages the `dynamic` keyword to give direct access to memory structures.

## How to use

Given an address and a ClrMD `ClrHeap` instance, you can get a dynamic proxy by calling `GetProxy`:

```C#
var proxy = heap.GetProxy(0x00001000);
```

Or all the instances of a given type:

```C#
// Using generics:
var proxies1 = heap.GetProxies<string>();

// Or writing the type name (useful if you don't reference it):
var proxies2 = heap.GetProxies("System.String");
```

From there, you can access any field like you would with a "real" object:

```C#
Console.WriteLine(proxy.Value);
Console.WriteLine((string)proxy.Child.Name);
Console.WriteLine(proxy.Description.Size.Width * proxy.Description.Size.Height);
```

Only fields are supported, but automatic properties are translated:

```C#
class SomeType
{
    private int _backingField;
    public int Field1 => _backingField;
    public int Field2 { get; }
}

var proxy = heap.GetProxies<SomeType>().First();

var value1 = proxy._backingField; // Calling proxy.Field1 is not supported
var value2 = proxy.Field2; // Automatically translated to <Field2>k__BackingField

```

[Primitive types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/built-in-types) are automatically converted:

```C#
class SomeType
{
    public int IntValue;
    public double DoubleValue;
}

var proxy = heap.GetProxies<SomeType>().First();

Console.WriteLine(proxy.IntValue.GetType()); // System.Int32
Console.WriteLine(proxy.DoubleValue.GetType()); // System.Double

```

Non-primitive proxies can be cast to string or [blittable structs](https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types):

```C#
struct BlittableStruct
{
    public int Value;
}

class SomeType
{
    public BlittableStruct StructValue;
    public DateTime DateTimeValue;
    public string StringValue;
}

var proxy = heap.GetProxies<SomeType>().First();

BlittableStruct structValue = (BlittableStruct)proxy.StructValue;
DateTime dateTimeValue = (DateTime)proxy.DateTimeValue;
string stringValue = (string)proxy.stringValue;

```

You can also enumerate the contents of arrays, get the length, or use an indexer:

```C#

class SomeType
{
    public int[] ArrayValue;
}

var proxy = heap.GetProxies<SomeType>().First();

Console.WriteLine("Length: " + proxy.ArrayValue.Length);
Console.WriteLine("First element: " + proxy.ArrayValue[0]);

foreach (var value in proxy.ArrayValue)
{
    Console.WriteLine(value);
}
```

To retrieve the address of a proxified object, explicitely cast it to ulong. Also, calling `.ToString()` on a proxy will return the address encoded in hexadecimal:

```C#
var proxy = heap.GetProxy(0x1000);
var address = (ulong)proxy;
Console.WriteLine("{0:x2}", address); // 0x1000
Console.WriteLine(proxy); // 0x1000
```

To retrieve the instance of ClrType, call `GetClrType()`:

```C#
ClrType type = proxy.GetClrType();
```

Check [the unit tests](https://github.com/kevingosse/DynaMD/blob/master/src/DynaMD.Tests/ProxyTest.cs) for more examples.
