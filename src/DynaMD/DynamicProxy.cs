using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.Diagnostics.Runtime;

namespace DynaMD
{
    internal class DynamicProxy : DynamicObject
    {
        private readonly ClrHeap _heap;
        private readonly ulong _address;

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

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var field = Type.GetFieldByName(binder.Name);

            if (field == null)
            {
                // The field wasn't found, it could be an autoproperty
                field = Type.GetFieldByName($"<{binder.Name}>k__BackingField");

                if (field == null)
                {
                    // Stil not found
                    throw new InvalidOperationException("Field not found: " + binder.Name);
                }
            }

            if (!field.HasSimpleValue)
            {
                result = new DynamicProxy(_heap, _address + (ulong)field.Offset, field.Type);

                return true;
            }

            result = field.GetValue(_address);

            // TODO: Use field.IsValueClass?
            if (result is ulong && field.Type.Name != "System.UInt64")
            {
                result = new DynamicProxy(_heap, (ulong)result);
            }

            return true;
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.ReturnType == typeof(IEnumerable))
            {
                result = Enumerate();
                return true;
            }

            result = null;
            return false;
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

        private IEnumerable<dynamic> Enumerate()
        {
            var length = Type.GetArrayLength(_address);

            for (int i = 0; i < length; i++)
            {
                yield return GetElementAt(i);
            }
        }

        private object GetElementAt(int index)
        {
            if (Type.HasSimpleValue)
            {
                return Type.GetArrayElementValue(_address, index);
            }

            return new DynamicProxy(_heap, Type.GetArrayElementAddress(_address, index));
        }
    }
}