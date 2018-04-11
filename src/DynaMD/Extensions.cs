using System.Collections.Generic;
using DynaMD;

// ReSharper disable once CheckNamespace
namespace Microsoft.Diagnostics.Runtime
{
    public static class Extensions
    {
        public static dynamic GetProxy(this ClrHeap heap, ulong address)
        {
            if (address == 0)
            {
                return null;
            }

            return new DynamicProxy(heap, address);
        }

        public static IEnumerable<dynamic> GetProxies<T>(this ClrHeap heap)
        {
            return GetProxies(heap, typeof(T).FullName);
        }

        public static IEnumerable<dynamic> GetProxies(this ClrHeap heap, string typeName)
        {
            foreach (var address in heap.EnumerateObjectAddresses())
            {
                if (heap.GetObjectType(address)?.Name == typeName)
                {
                    yield return heap.GetProxy(address);
                }
            }
        }
    }
}