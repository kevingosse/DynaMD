using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.Runtime;

namespace DynaMD
{
    internal class DynamicProxy : DynamicObject
    {
        private readonly bool _interior;
        private readonly ulong _address;
        private readonly ClrType _type;

        public DynamicProxy(ulong address, ClrType type)
            : this(address, type, false)
        {
        }

        private DynamicProxy(ulong address, ClrType type, bool interior)
        {
            _address = address;
            _type = type;
            _interior = interior;
        }

        protected ClrType Type => _type;

        protected ClrObject ClrObject => new ClrObject(_address, Type);

        protected ulong AddressWithoutHeader => _interior ? _address : _address + (ulong)_type.Heap.Runtime.DataTarget.DataReader.PointerSize;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (binder.Name == "Length" && Type.IsArray)
            {
                result = ClrObject.AsArray().Length;
                return true;
            }

            var field = Type.GetFieldByName(binder.Name);

            if (field == null)
            {
                // The field wasn't found, it could be an autoproperty
                field = Type.GetFieldByName($"<{binder.Name}>k__BackingField");

                if (field == null)
                {
                    // Still not found
                    result = null;
                    return false;
                }
            }

            if (field.Type.IsValueType)
            {
                var value = field.ReadStruct(_address, _interior);

                result = ConvertIfPrimitive(value);

            }
            else
            {
                var value = field.ReadObject(_address, _interior);

                if (value.Address == 0)
                {
                    result = null;
                }
                else
                {
                    result = value.AsDynamic();
                }
            }

            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (binder.Name == "GetClrType")
            {
                result = Type;
                return true;
            }

            if (binder.Name == "Is")
            {
                if (args.Length != 1)
                {
                    throw new ArgumentException("Missing argument 'type'");
                }

                if (!(args[0] is string expectedType))
                {
                    throw new ArgumentException("The 'type' argument must be a string");
                }

                result = Type.Name == expectedType;
                return true;
            }

            return base.TryInvokeMember(binder, args, out result);
        }

        public static bool IsBlittable(Type type, bool allowArrays = true)
        {
            if (type.IsArray)
            {
                return allowArrays && IsBlittable(type.GetElementType());
            }

            if (!type.IsValueType)
            {
                return false;
            }

            if (type.IsPrimitive)
            {
                return true;
            }

            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(f => IsBlittable(f.FieldType, false));
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.ReturnType == typeof(ulong))
            {
                result = _address;
                return true;
            }

            if (binder.ReturnType == typeof(string) && Type.IsString)
            {
                result = ClrObject.AsString();
                return true;
            }

            if (binder.ReturnType.FullName == Type.Name && IsBlittable(binder.ReturnType))
            {
                if (binder.ReturnType.IsArray)
                {
                    var clrArray = ClrObject.AsArray();

                    if (clrArray.Length == 0)
                    {
                        result = Array.CreateInstance(binder.ReturnType.GetElementType(), 0);
                    }
                    else
                    {
                        var methodInfo = clrArray.GetType().GetMethod("ReadValues").MakeGenericMethod(binder.ReturnType.GetElementType());
                        result = methodInfo.Invoke(clrArray, new object[] { 0, clrArray.Length });
                    }

                    return true;
                }

                result = Read(AddressWithoutHeader, binder.ReturnType);

                return true;
            }

            IEnumerable<dynamic> Enumerate()
            {
                var array = ClrObject.AsArray();

                var length = array.Length;

                for (int i = 0; i < length; i++)
                {
                    yield return GetElementAt(array, i);
                }
            }

            if (binder.ReturnType == typeof(IEnumerable))
            {
                result = Enumerate();
                return true;
            }

            throw new InvalidCastException("Can only cast array and blittable types, or to ulong to retrieve the address");
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (!Type.IsArray)
            {
                throw new NotSupportedException($"{Type.Name} is not an array");
            }

            var index = (int)indexes[0];

            result = GetElementAt(new ClrArray(_address, Type), index);
            return true;
        }

        public override string ToString() => _address.ToString("x2");

        private static bool IsReference(object result, ClrType type)
        {
            return result != null && !(result is string) && type.IsObjectReference;
        }

        private static DynamicProxy GetProxy(ClrHeap heap, ulong address)
        {
            return address == 0 ? null : heap.GetProxy(address);
        }

        // ReSharper disable once UnusedMember.Local - Used through reflection
        private static unsafe object Read<T>(byte[] buffer)
        {
            fixed (byte* b = buffer)
            {
                return Unsafe.Read<T>(b);
            }
        }

        private object ConvertIfPrimitive(ClrValueType value)
        {
            if (value.Type.IsPrimitive)
            {
                var realType = System.Type.GetType(value.Type.Name);
                return Read(value.Address, realType);
            }

            return new DynamicProxy(value.Address, value.Type, true);
        }

        private object Read(ulong address, Type type)
        {
            var m = typeof(IMemoryReader).GetMethod("Read", new[] { typeof(ulong) }).MakeGenericMethod(type);

            return m.Invoke(Type.ClrObjectHelpers.DataReader, new object[] { address });
        }

        private object GetElementAt(ClrArray array, int index)
        {
            if (array.Type.ComponentType.IsValueType)
            {
                return ConvertIfPrimitive(array.GetStructValue(index));
            }

            return array.GetObjectValue(index).AsDynamic();
        }
    }
}