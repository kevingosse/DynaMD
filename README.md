# DynaMD
Helper objects to browse complex structures returned ClrMD

The library leverages the **dynamic** keyword to give easy access to memory structures.

Given an address and a ClrMD **ClrHeap** instance, you can get a dynamic proxy by calling GetProxy:

```C#
var proxy = heap.GetProxy(0x00001000);
```

From there, you can access any field like you would with a "real" object:

```C#
Console.WriteLine(proxy.Value);
Console.WriteLine(proxy.Child.Name);
Console.WriteLine(proxy.Description.Size.Width * proxy.Description.Size.Height);
```

You can also enumerate the contents of arrays:

```C#
foreach (var value in proxy.Values)
{
    Console.WriteLine(value);
}
```

To retrieve the address of a proxified object, explicitely cast it to ulong:

```C#
var address = (ulong)proxy.Child;
```

Check [the unit tests](https://github.com/KooKiz/DynaMD/blob/master/src/DynaMD.Tests/ProxyTest.cs) for more examples.
