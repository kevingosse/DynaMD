using DynaMD;

// ReSharper disable once CheckNamespace
namespace Microsoft.Diagnostics.Runtime
{
    public static class Extensions
    {
        public static dynamic GetProxy(this ClrHeap heap, ulong address)
        {
            return new DynamicProxy(heap, address);
        }
    }
}