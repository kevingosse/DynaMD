using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.Diagnostics.Runtime;

namespace DynaMD
{
    internal class DynamicProxy : DynamicObject
    {
        private readonly ClrHeap _heap;
        private readonly ulong _address;

        private readonly bool _interior;
        private ClrType _type;

        public DynamicProxy(ClrHeap heap, ulong address)
        {
            _heap = heap;
            _address = address;
        }

        private DynamicProxy(ClrHeap heap, ulong address, ClrType overrideType)
            : this(heap, address)
        {
            _type = overrideType;
            _interior = true;
        }

        protected ClrType Type
        {
            get
            {
                if (_type == null)
                {
                    _type = _heap.GetObjectType(_address);
                }

                return _type;
            }
        }

        protected ulong AddressWithoutHeader => _interior ? _address : _address + (ulong)_heap.PointerSize;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (binder.Name == "Length" && Type.IsArray)
            {
                result = Type.GetArrayLength(_address);
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

            if (!field.HasSimpleValue)
            {
                result = LinkToStruct(field);

                return true;
            }

            result = field.GetValue(_address, _interior);

            if (IsReference(result, field.Type))
            {
                result = GetProxy(_heap, (ulong)result);
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

        public static bool IsBlittable(Type type)
        {
            if (type.IsArray)
            {
                var elem = type.GetElementType();
                return elem.IsValueType && IsBlittable(elem);
            }
            try
            {
                object instance = FormatterServices.GetUninitializedObject(type);
                GCHandle.Alloc(instance, GCHandleType.Pinned).Free();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.ReturnType == typeof(ulong))
            {
                result = _address;
                return true;
            }

            if (binder.ReturnType.FullName == Type.Name && IsBlittable(binder.ReturnType))
            {
                if (binder.ReturnType.IsArray)
                {
                    result = MarshalToArray(binder.ReturnType);
                    return true;
                }


                result = MarshalToStruct(binder.ReturnType);
                return true;

            }

            IEnumerable<dynamic> Enumerate()
            {
                var length = Type.GetArrayLength(_address);

                for (int i = 0; i < length; i++)
                {
                    yield return GetElementAt(i);
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

            result = GetElementAt(index);
            return true;
        }

        private static bool IsReference(object result, ClrType type)
        {
            return result != null && !(result is string) && type.IsObjectReference;
        }

        private static DynamicProxy GetProxy(ClrHeap heap, ulong address)
        {
            return address == 0 ? null : new DynamicProxy(heap, address);
        }

        private DynamicProxy LinkToStruct(ClrField field)
        {
            var childAddress = AddressWithoutHeader + (ulong)field.Offset;

            return new DynamicProxy(_heap, childAddress, field.Type);
        }

        private object GetElementAt(int index)
        {
            if (Type.ComponentType.HasSimpleValue)
            {
                var result = Type.GetArrayElementValue(_address, index);

                if (IsReference(result, Type.ComponentType))
                {
                    return GetProxy(_heap, (ulong)result);
                }

                return Type.GetArrayElementValue(_address, index);
            }

            return new DynamicProxy(_heap, Type.GetArrayElementAddress(_address, index), Type.ComponentType);
        }

        private unsafe object MarshalToStruct(Type destinationType)
        {
            var buffer = new byte[Type.BaseSize];

            _heap.ReadMemory(AddressWithoutHeader, buffer, 0, buffer.Length);

            fixed (byte* p = buffer)
            {
                return Marshal.PtrToStructure(new IntPtr(p), destinationType);
            }
        }

        private object MarshalToArray(Type arrayType)
        {
            var length = Type.GetArrayLength(_address);
            var buffer = new byte[Type.ElementSize * length];

            var array = Activator.CreateInstance(arrayType, length);

            if (length == 0)
            {
                return array;
            }

            var arrayContentAddress = Type.GetArrayElementAddress(_address, 0);

            _heap.ReadMemory(arrayContentAddress, buffer, 0, buffer.Length);

            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);

            try
            {
                Marshal.Copy(buffer, 0, handle.AddrOfPinnedObject(), buffer.Length);
            }
            finally
            {
                handle.Free();
            }

            return array;
        }
    }
}